(() => {
  const bridge = window.chrome.webview;
  let editor = null;
  let suppressInput = false;
  let inputTimer = null;
  let pendingNestedListRetype = false;
  let backwardDeleteHandledInKeydown = false;
  let lastEmptyNestedListItem = null;
  let nestedDeleteRecovery = null;
  let outlineResizeObserver = null;
  let alignmentObserver = null;
  let alignmentRefreshScheduled = false;
  let alignmentHydrating = false;

  const assetEditorPrefix = "https://swdt-assets.local/";
  const assetPersistedPattern = /swdt-asset:\/\/([0-9a-f-]{36})\/([^\s)\]}"']*)/gi;
  const assetEditorPattern = /https:\/\/swdt-assets\.local\/([0-9a-f-]{36})\/([^\s)\]}"']*)/gi;

  function toEditorMarkdown(value) {
    return (value || "").replace(assetPersistedPattern, `${assetEditorPrefix}$1/$2`);
  }

  function fromEditorMarkdown(value) {
    return (value || "").replace(assetEditorPattern, "swdt-asset://$1/$2");
  }

  function currentMarkdown() {
    if (!editor) return "";
    const alignedValue = serializeAlignedMarkdown();
    return fromEditorMarkdown(alignedValue ?? editor.getValue());
  }

  function post(message) {
    bridge.postMessage(message);
  }

  function emitInput() {
    if (suppressInput || !editor) return;
    clearTimeout(inputTimer);
    post({ type: "content", value: currentMarkdown() });
  }

  function uploadFiles(files) {
    Array.from(files || []).forEach(file => {
      if (!/^image\/(png|jpeg|gif|webp)$/i.test(file.type)) return;
      const reader = new FileReader();
      reader.onload = () => {
        const value = String(reader.result || "");
        const comma = value.indexOf(",");
        if (comma < 0) return;
        post({
          type: "asset",
          requestId: crypto.randomUUID(),
          fileName: file.name || "image.png",
          mediaType: file.type,
          dataBase64: value.substring(comma + 1)
        });
      };
      reader.readAsDataURL(file);
    });
    return null;
  }

  function completePendingListShortcut(event) {
    if (!editor || editor.getCurrentMode() !== "ir" || event.key !== " " ||
        event.isComposing || event.ctrlKey || event.altKey || event.metaKey || event.shiftKey) {
      return;
    }

    const selection = window.getSelection();
    if (!selection || !selection.isCollapsed || selection.rangeCount === 0) return;
    const anchor = selection.anchorNode;
    const anchorElement = anchor && (anchor.nodeType === Node.ELEMENT_NODE ? anchor : anchor.parentElement);
    const block = anchorElement && anchorElement.closest(".vditor-ir [data-block='0']");
    if (!block) return;

    const prefixRange = document.createRange();
    prefixRange.selectNodeContents(block);
    try {
      prefixRange.setEnd(anchor, selection.anchorOffset);
    } catch {
      return;
    }

    const marker = prefixRange.toString().replace(/\u200b/g, "");
    if (marker !== "*" && !/^\d+\.$/.test(marker)) return;

    // Vditor recognizes list markers such as "* " and "1. " only when the
    // following character arrives. Insert and remove a temporary character in
    // the same task so its native IR parser performs the conversion immediately
    // while the list item stays empty.
    setTimeout(() => {
      const currentSelection = window.getSelection();
      if (!currentSelection || !currentSelection.isCollapsed || currentSelection.rangeCount === 0) return;
      document.execCommand("insertText", false, "x");
      document.execCommand("delete", false);
    }, 0);
  }

  const invisibleEditorCharacterPattern = /[\u200b\u200c\u200d\u2060\ufeff]/g;

  function preventEditorEvent(event) {
    event.preventDefault();
    event.stopImmediatePropagation();
  }

  function getSelectionListItem() {
    const selection = window.getSelection();
    if (!selection || !selection.isCollapsed || selection.rangeCount === 0) return null;
    const anchor = selection.anchorNode;
    const anchorElement = anchor && (anchor.nodeType === Node.ELEMENT_NODE ? anchor : anchor.parentElement);
    return anchorElement && anchorElement.closest(".vditor-ir li");
  }

  function getPendingNestedListItem() {
    return document.querySelector(".vditor-ir li[data-swdt-retype-list='true']");
  }

  function getListItemOwnText(listItem) {
    if (!listItem) return "";
    const copy = listItem.cloneNode(true);
    copy.querySelectorAll("ul, ol, br, wbr").forEach(element => element.remove());
    return copy.textContent.replace(invisibleEditorCharacterPattern, "").trim();
  }

  function isSemanticallyEmptyListItem(listItem) {
    return getListItemOwnText(listItem) === "";
  }

  function captureListItemLocation(listItem) {
    if (!editor || !listItem) return null;
    const surface = editor.vditor.ir.element;
    const itemPath = [];
    let currentItem = listItem;
    while (currentItem) {
      const parentList = currentItem.parentElement;
      if (!parentList || !/^(UL|OL)$/.test(parentList.tagName)) return null;
      const siblings = Array.from(parentList.children).filter(element => element.tagName === "LI");
      itemPath.unshift(siblings.indexOf(currentItem));
      if (parentList.parentElement === surface) {
        return {
          blockIndex: Array.from(surface.children).indexOf(parentList),
          itemPath
        };
      }
      currentItem = parentList.parentElement?.closest("li") || null;
    }
    return null;
  }

  function resolveListItemLocation(location, ownText) {
    if (!editor) return null;
    const surface = editor.vditor.ir.element;
    if (location && location.blockIndex >= 0) {
      let list = surface.children[location.blockIndex];
      let item = null;
      for (let index = 0; list && index < location.itemPath.length; index += 1) {
        const items = Array.from(list.children).filter(element => element.tagName === "LI");
        item = items[location.itemPath[index]] || null;
        list = index < location.itemPath.length - 1
          ? Array.from(item?.children || []).find(element => /^(UL|OL)$/.test(element.tagName))
          : null;
      }
      if (item) return item;
    }

    return Array.from(surface.querySelectorAll("li"))
      .find(item => getListItemOwnText(item) === ownText) || null;
  }

  function resetPendingNestedListRetype() {
    const pendingItem = getPendingNestedListItem();
    if (pendingItem) {
      pendingItem.style.removeProperty("list-style-type");
      pendingItem.removeAttribute("data-swdt-retype-list");
    }
    pendingNestedListRetype = false;
    lastEmptyNestedListItem = null;
    nestedDeleteRecovery = null;
  }

  function placeCaretAtEnd(element) {
    const selection = window.getSelection();
    if (!selection) return;
    const range = document.createRange();
    range.selectNodeContents(element);
    range.collapse(false);
    selection.removeAllRanges();
    selection.addRange(range);
  }

  function typePendingNestedListMarker(event, value = event.key) {
    if (!pendingNestedListRetype || !editor || editor.getCurrentMode() !== "ir" ||
        event.ctrlKey || event.altKey || event.metaKey) {
      return false;
    }

    const listItem = getSelectionListItem();
    if (!listItem || listItem !== getPendingNestedListItem()) return false;

    if (/^[\d.]$/.test(value)) {
      preventEditorEvent(event);
      listItem.append(document.createTextNode(value));
      placeCaretAtEnd(listItem);
      emitInput();
      return true;
    }

    if (value === "Backspace" && listItem.textContent.length > 0) {
      preventEditorEvent(event);
      listItem.textContent = listItem.textContent.slice(0, -1);
      placeCaretAtEnd(listItem);
      emitInput();
      return true;
    }

    return false;
  }

  function completePendingNestedListShortcut(event, value = event.key) {
    if (!pendingNestedListRetype || !editor || editor.getCurrentMode() !== "ir" || value !== " " ||
        event.ctrlKey || event.altKey || event.metaKey || event.shiftKey) {
      return false;
    }

    const selection = window.getSelection();
    const listItem = getSelectionListItem();
    const sourceList = listItem && listItem.parentElement;
    const parentListItem = sourceList && sourceList.parentElement && sourceList.parentElement.closest(".vditor-ir li");
    if (!selection || !listItem || listItem !== getPendingNestedListItem() || !sourceList ||
        !parentListItem || !/^(UL|OL)$/.test(sourceList.tagName)) {
      return false;
    }

    const marker = listItem.textContent.replace(invisibleEditorCharacterPattern, "").trim();
    const targetTagName = /^\d+\.$/.test(marker) ? "OL" : (marker === "*" ? "UL" : null);
    if (!targetTagName) return false;

    preventEditorEvent(event);
    const targetList = document.createElement(targetTagName.toLowerCase());
    targetList.setAttribute("data-tight", sourceList.getAttribute("data-tight") || "true");
    targetList.setAttribute("data-marker", targetTagName === "OL" ? marker : "*");
    targetList.setAttribute("data-block", "0");
    sourceList.insertAdjacentElement("afterend", targetList);

    let itemToMove = listItem;
    while (itemToMove) {
      const nextItem = itemToMove.nextElementSibling;
      targetList.appendChild(itemToMove);
      itemToMove = nextItem;
    }
    if (!sourceList.children.length) sourceList.remove();

    listItem.replaceChildren();
    listItem.setAttribute("data-marker", targetTagName === "OL" ? marker : "*");
    listItem.style.removeProperty("list-style-type");
    listItem.removeAttribute("data-swdt-retype-list");
    pendingNestedListRetype = false;
    nestedDeleteRecovery = null;
    placeCaretAtEnd(listItem);
    emitInput();
    return true;
  }

  function handleBackwardDelete(event) {
    if (!editor || editor.getCurrentMode() !== "ir") return false;

    const selection = window.getSelection();
    let listItem = getSelectionListItem();
    if ((!listItem || !isSemanticallyEmptyListItem(listItem)) &&
        lastEmptyNestedListItem && lastEmptyNestedListItem.isConnected) {
      listItem = lastEmptyNestedListItem;
    }
    if (!selection || !listItem || !isSemanticallyEmptyListItem(listItem)) return false;

    const list = listItem.parentElement;
    if (!list || !/^(UL|OL)$/.test(list.tagName)) return false;
    const parentListItem = list.parentElement && list.parentElement.closest(".vditor-ir li");

    if (parentListItem) {
      preventEditorEvent(event);
      if (pendingNestedListRetype && listItem === getPendingNestedListItem()) {
        nestedDeleteRecovery = null;
        resetPendingNestedListRetype();
        listItem.remove();
        if (!list.children.length) list.remove();
        placeCaretAtEnd(parentListItem);
        editor.insertEmptyBlock("afterend");
        return true;
      }

      resetPendingNestedListRetype();
      nestedDeleteRecovery = {
        listItem,
        list,
        parentListItem,
        parentLocation: captureListItemLocation(parentListItem),
        parentOwnText: getListItemOwnText(parentListItem),
        parentHadParagraph: parentListItem.firstElementChild?.tagName === "P",
        listTagName: list.tagName,
        tight: list.getAttribute("data-tight") || "true",
        marker: list.getAttribute("data-marker") || (list.tagName === "OL" ? "1." : "*")
      };
      listItem.replaceChildren();
      listItem.setAttribute("data-swdt-retype-list", "true");
      listItem.style.setProperty("list-style-type", "none");
      pendingNestedListRetype = true;
      lastEmptyNestedListItem = listItem;
      placeCaretAtEnd(listItem);
      return true;
    }

    const previousItem = listItem.previousElementSibling;
    if (!previousItem || list.getAttribute("data-block") !== "0") return false;
    preventEditorEvent(event);
    listItem.remove();
    placeCaretAtEnd(previousItem);
    editor.insertEmptyBlock("afterend");
    return true;
  }

  function markBackwardDeleteHandledInKeydown() {
    backwardDeleteHandledInKeydown = true;
    setTimeout(() => {
      backwardDeleteHandledInKeydown = false;
      if (getPendingNestedListItem()?.isConnected) nestedDeleteRecovery = null;
    }, 0);
  }

  function isBackwardDeleteInputType(inputType) {
    return typeof inputType === "string" &&
      (inputType === "deleteCompositionText" || /^delete.*Backward$/i.test(inputType));
  }

  function recoverNestedListAfterNativeDelete() {
    const recovery = nestedDeleteRecovery;
    nestedDeleteRecovery = null;
    if (!recovery) return;

    const parentListItem = recovery.parentListItem?.isConnected
      ? recovery.parentListItem
      : resolveListItemLocation(recovery.parentLocation, recovery.parentOwnText);
    if (!parentListItem) return;

    const currentPendingItem = getPendingNestedListItem();
    if (currentPendingItem?.isConnected) {
      placeCaretAtEnd(currentPendingItem);
      return;
    }

    let list = recovery.list;
    if (!list?.isConnected) {
      list = document.createElement(recovery.listTagName.toLowerCase());
      list.setAttribute("data-tight", recovery.tight);
      list.setAttribute("data-marker", recovery.marker);
      list.setAttribute("data-block", "0");
      const emptyTrailingParagraph = parentListItem.lastElementChild;
      if (emptyTrailingParagraph?.tagName === "P" &&
          emptyTrailingParagraph.textContent.replace(invisibleEditorCharacterPattern, "").trim() === "") {
        emptyTrailingParagraph.remove();
      }
      const parentTextParagraph = parentListItem.firstElementChild;
      if (!recovery.parentHadParagraph && parentTextParagraph?.tagName === "P") {
        while (parentTextParagraph.firstChild) {
          parentListItem.insertBefore(parentTextParagraph.firstChild, parentTextParagraph);
        }
        parentTextParagraph.remove();
      }
      parentListItem.appendChild(list);
    }

    const listItem = document.createElement("li");
    listItem.setAttribute("data-marker", recovery.marker);
    listItem.setAttribute("data-swdt-retype-list", "true");
    listItem.style.setProperty("list-style-type", "none");
    list.appendChild(listItem);
    pendingNestedListRetype = true;
    lastEmptyNestedListItem = listItem;
    placeCaretAtEnd(listItem);
  }

  function setOutlineItemTitle(item) {
    if (!(item instanceof HTMLElement)) return;
    const title = (item.textContent || "").trim();
    if (title && item.scrollWidth > item.clientWidth + 1) {
      item.setAttribute("title", title);
    } else {
      item.removeAttribute("title");
    }
  }

  function bindOutlineResize(outlineElement, language) {
    const contentElement = outlineElement.parentElement;
    if (!contentElement) return;

    let resizer = contentElement.querySelector(":scope > .swdt-outline-resizer");
    if (!resizer) {
      resizer = document.createElement("div");
      resizer.className = "swdt-outline-resizer";
      resizer.setAttribute("role", "separator");
      resizer.setAttribute("aria-orientation", "vertical");
      resizer.setAttribute("aria-label", language === "zh_CN" ? "调整目录宽度" : "Resize outline");
      resizer.tabIndex = 0;
      contentElement.appendChild(resizer);
    }

    const minimumWidth = 120;
    const minimumEditorWidth = 160;
    const maximumWidth = () => Math.max(
      minimumWidth,
      Math.min(480, contentElement.clientWidth - minimumEditorWidth));
    const clampWidth = value => Math.min(maximumWidth(), Math.max(minimumWidth, value));
    const updateHandle = () => {
      resizer.style.left = `${outlineElement.offsetLeft + outlineElement.offsetWidth}px`;
      resizer.setAttribute("aria-valuemin", String(minimumWidth));
      resizer.setAttribute("aria-valuemax", String(maximumWidth()));
      resizer.setAttribute("aria-valuenow", String(Math.round(outlineElement.offsetWidth)));
    };
    const applyWidth = value => {
      outlineElement.style.setProperty("width", `${Math.round(clampWidth(value))}px`, "important");
      updateHandle();
    };

    resizer.addEventListener("pointerdown", event => {
      if (event.button !== 0) return;
      event.preventDefault();
      event.stopPropagation();
      const startX = event.clientX;
      const startWidth = outlineElement.getBoundingClientRect().width;
      resizer.setPointerCapture(event.pointerId);
      resizer.classList.add("swdt-active");
      document.body.classList.add("swdt-outline-resizing");

      const move = moveEvent => {
        if (moveEvent.pointerId !== event.pointerId) return;
        applyWidth(startWidth + moveEvent.clientX - startX);
      };
      const finish = finishEvent => {
        if (finishEvent.pointerId !== event.pointerId) return;
        resizer.removeEventListener("pointermove", move);
        resizer.removeEventListener("pointerup", finish);
        resizer.removeEventListener("pointercancel", finish);
        resizer.classList.remove("swdt-active");
        document.body.classList.remove("swdt-outline-resizing");
        updateHandle();
      };
      resizer.addEventListener("pointermove", move);
      resizer.addEventListener("pointerup", finish);
      resizer.addEventListener("pointercancel", finish);
    });

    resizer.addEventListener("keydown", event => {
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      event.preventDefault();
      const delta = event.key === "ArrowLeft" ? -16 : 16;
      applyWidth(outlineElement.getBoundingClientRect().width + delta);
    });

    outlineResizeObserver?.disconnect();
    outlineResizeObserver = new ResizeObserver(() => {
      const currentWidth = outlineElement.getBoundingClientRect().width;
      if (currentWidth > maximumWidth()) {
        applyWidth(currentWidth);
      } else {
        updateHandle();
      }
    });
    outlineResizeObserver.observe(contentElement);
    outlineResizeObserver.observe(outlineElement);
    updateHandle();
  }

  function bindOutlineNavigation(language) {
    if (!editor || !editor.vditor || !editor.vditor.outline) return;
    const outlineElement = editor.vditor.outline.element;
    if (outlineElement.dataset.swdtNavigationBound === "true") return;
    outlineElement.dataset.swdtNavigationBound = "true";

    bindOutlineResize(outlineElement, language);

    outlineElement.addEventListener("mouseover", event => {
      const item = event.target.closest && event.target.closest("li > span > span");
      if (item && outlineElement.contains(item)) setOutlineItemTitle(item);
    });

    // The Vditor outline body is rebuilt as headings change. Listen on the stable
    // container in capture phase so every newly rendered item keeps navigation.
    outlineElement.addEventListener("click", event => {
      if (event.target.closest && event.target.closest(".vditor-outline__action")) return;
      const item = event.target.closest && event.target.closest("[data-target-id]");
      if (!item || !outlineElement.contains(item)) return;

      const targetId = item.getAttribute("data-target-id");
      const target = targetId ? document.getElementById(targetId) : null;
      if (!target) return;

      event.preventDefault();
      event.stopPropagation();
      const mode = editor.getCurrentMode();
      const surface = editor.vditor[mode] && editor.vditor[mode].element;
      if (surface && surface.contains(target)) {
        surface.scrollTo({ top: Math.max(0, target.offsetTop - 12), behavior: "smooth" });
      } else {
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    }, true);
  }

  const alignmentTargetSelector = "p, h1, h2, h3, h4, h5, h6, li, td, th";
  const alignmentOpeningPattern = /^<span style="display:block;text-align:(center|right)">$/i;

  function getHtmlInlineMarkup(node) {
    if (!(node instanceof HTMLElement) || node.getAttribute("data-type") !== "html-inline") return "";
    const marker = node.querySelector(":scope > code.vditor-ir__marker");
    return (marker?.textContent || "").trim();
  }

  function isSupportedAlignmentTarget(target) {
    if (!(target instanceof HTMLElement) || !target.matches(alignmentTargetSelector)) return false;
    const surface = document.querySelector(".vditor-ir");
    return Boolean(surface?.contains(target)) &&
      !target.closest("[data-type='code-block'], [data-type='html-block'], .vditor-ir__preview");
  }

  function getAlignmentTarget(node) {
    const element = node instanceof Element ? node : node?.parentElement;
    const target = element?.closest(alignmentTargetSelector);
    return isSupportedAlignmentTarget(target) ? target : null;
  }

  function getAlignmentParts(target) {
    if (!isSupportedAlignmentTarget(target)) return null;
    const children = Array.from(target.children);
    let opening = null;
    let alignment = "";
    let openingIndex = -1;
    for (let index = 0; index < children.length; index++) {
      const match = getHtmlInlineMarkup(children[index]).match(alignmentOpeningPattern);
      if (!match) continue;
      opening = children[index];
      alignment = match[1].toLowerCase();
      openingIndex = index;
      break;
    }
    if (!opening) return null;

    for (let index = children.length - 1; index > openingIndex; index--) {
      if (getHtmlInlineMarkup(children[index]).toLowerCase() === "</span>") {
        return { opening, closing: children[index], alignment };
      }
    }
    return null;
  }

  function clearAlignmentDecoration(target) {
    if (!(target instanceof HTMLElement)) return;
    if (target.hasAttribute("data-swdt-alignment")) {
      target.style.removeProperty("text-align");
      target.removeAttribute("data-swdt-alignment");
    }
  }

  function hydrateAlignments() {
    if (alignmentHydrating) return;
    const surface = document.querySelector(".vditor-ir");
    if (!surface) return;
    alignmentHydrating = true;
    try {
      surface.querySelectorAll(alignmentTargetSelector).forEach(target => {
        const parts = getAlignmentParts(target);
        const alignment = parts?.alignment || target.getAttribute("data-swdt-alignment");
        if (parts) {
          parts.opening.remove();
          parts.closing.remove();
        }
        if (alignment === "center" || alignment === "right") {
          target.setAttribute("data-swdt-alignment", alignment);
          target.style.setProperty("text-align", alignment);
        }
      });
    } finally {
      alignmentHydrating = false;
    }
  }

  function scheduleAlignmentHydration() {
    if (alignmentRefreshScheduled) return;
    alignmentRefreshScheduled = true;
    requestAnimationFrame(() => {
      alignmentRefreshScheduled = false;
      hydrateAlignments();
    });
  }

  function observeAlignments() {
    alignmentObserver?.disconnect();
    const surface = document.querySelector(".vditor-ir");
    if (!surface) return;
    hydrateAlignments();
    alignmentObserver = new MutationObserver(() => {
      if (!alignmentHydrating) scheduleAlignmentHydration();
    });
    alignmentObserver.observe(surface, { childList: true, subtree: true });
  }

  function rangeIntersectsNode(range, node) {
    try {
      return range.intersectsNode(node);
    } catch {
      return false;
    }
  }

  function getSelectedAlignmentTargets() {
    const selection = window.getSelection();
    const surface = document.querySelector(".vditor-ir");
    if (!selection || !surface || selection.rangeCount === 0) return [];
    const range = selection.getRangeAt(0);
    if (!surface.contains(range.commonAncestorContainer)) return [];

    const targets = new Set();
    const startTarget = getAlignmentTarget(range.startContainer);
    const endTarget = getAlignmentTarget(range.endContainer);
    if (startTarget) targets.add(startTarget);
    if (endTarget) targets.add(endTarget);

    if (!selection.isCollapsed) {
      const walker = document.createTreeWalker(
        surface,
        NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT,
        {
          acceptNode: node => {
            if (node.nodeType === Node.TEXT_NODE) {
              return node.textContent?.replace(invisibleEditorCharacterPattern, "").length
                ? NodeFilter.FILTER_ACCEPT
                : NodeFilter.FILTER_SKIP;
            }
            return node instanceof HTMLImageElement ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
          }
        });
      let node = walker.nextNode();
      while (node) {
        if (rangeIntersectsNode(range, node)) {
          const target = getAlignmentTarget(node);
          if (target) targets.add(target);
        }
        node = walker.nextNode();
      }
    }

    return Array.from(targets).sort((left, right) =>
      left.compareDocumentPosition(right) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1);
  }

  function getDocumentAlignmentTargets() {
    const surface = document.querySelector(".vditor-ir");
    if (!surface) return [];
    return Array.from(surface.querySelectorAll(alignmentTargetSelector)).filter(target => {
      if (!isSupportedAlignmentTarget(target)) return false;
      if (!target.matches("li, td, th")) return true;
      return !Array.from(target.children).some(child => child.matches("p, h1, h2, h3, h4, h5, h6"));
    });
  }

  function createAlignmentMarker(markup) {
    const wrapper = document.createElement("span");
    wrapper.setAttribute("data-type", "html-inline");
    wrapper.setAttribute("contenteditable", "false");
    wrapper.className = "vditor-ir__node swdt-alignment-marker";
    const marker = document.createElement("code");
    marker.className = "vditor-ir__marker";
    marker.textContent = markup;
    wrapper.appendChild(marker);
    return wrapper;
  }

  function removeCanonicalAlignmentMarkers(target, removeOrphanClosing = false) {
    const children = Array.from(target.children);
    let removedOpening = false;
    children.forEach(child => {
      if (alignmentOpeningPattern.test(getHtmlInlineMarkup(child))) {
        child.remove();
        removedOpening = true;
      }
    });
    if (removedOpening || removeOrphanClosing) {
      const remaining = Array.from(target.children);
      for (let index = remaining.length - 1; index >= 0; index--) {
        if (getHtmlInlineMarkup(remaining[index]).toLowerCase() === "</span>") {
          remaining[index].remove();
          break;
        }
      }
    }
    clearAlignmentDecoration(target);
  }

  function getAlignmentStartReference(target) {
    let reference = target.firstChild;
    while (reference instanceof HTMLElement && reference.matches("[data-type='heading-marker']")) {
      reference = reference.nextSibling;
    }

    const taskInput = target.matches("li.vditor-task") && reference instanceof HTMLInputElement
      ? reference
      : null;
    if (!taskInput) return reference;
    reference = taskInput.nextSibling;
    while (reference?.nodeType === Node.TEXT_NODE) {
      const text = reference.textContent || "";
      if (/^\s*$/.test(text)) {
        reference = reference.nextSibling;
        continue;
      }
      const leadingWhitespace = text.match(/^\s+/)?.[0] || "";
      if (leadingWhitespace) {
        reference.textContent = text.substring(leadingWhitespace.length);
        target.insertBefore(document.createTextNode(leadingWhitespace), reference);
      }
      break;
    }
    return reference;
  }

  function applyAlignmentToTarget(target, alignment, repairSplit = false) {
    if (!isSupportedAlignmentTarget(target)) return;
    removeCanonicalAlignmentMarkers(target, repairSplit);
    if (alignment === "left") return;
    target.setAttribute("data-swdt-alignment", alignment);
    target.style.setProperty("text-align", alignment);
  }

  function hasSerializableAlignmentContent(target) {
    const content = target.cloneNode(true);
    if (content.matches("li")) {
      content.querySelectorAll(":scope > ul, :scope > ol").forEach(list => list.remove());
      content.querySelectorAll(":scope > input[type='checkbox']").forEach(input => input.remove());
    }
    const text = (content.textContent || "").replace(invisibleEditorCharacterPattern, "").trim();
    return Boolean(text || content.querySelector("img, video, audio"));
  }

  function serializeAlignedMarkdown() {
    const surface = document.querySelector(".vditor-ir pre");
    if (!surface) return null;
    const sourceTargets = Array.from(surface.querySelectorAll(alignmentTargetSelector));
    if (!sourceTargets.some(target => target.hasAttribute("data-swdt-alignment"))) return null;

    const copy = surface.cloneNode(true);
    const copyTargets = Array.from(copy.querySelectorAll(alignmentTargetSelector));
    sourceTargets.forEach((sourceTarget, index) => {
      const copyTarget = copyTargets[index];
      if (!copyTarget) return;
      const alignment = sourceTarget.getAttribute("data-swdt-alignment");
      copyTarget.removeAttribute("data-swdt-alignment");
      copyTarget.style.removeProperty("text-align");
      if ((alignment !== "center" && alignment !== "right") ||
          !hasSerializableAlignmentContent(copyTarget)) return;

      const opening = createAlignmentMarker(`<span style="display:block;text-align:${alignment}">`);
      const closing = createAlignmentMarker("</span>");
      const startReference = getAlignmentStartReference(copyTarget);
      copyTarget.insertBefore(opening, startReference);
      const nestedList = copyTarget.matches("li")
        ? Array.from(copyTarget.children).find(child => child.matches("ul, ol"))
        : null;
      copyTarget.insertBefore(closing, nestedList || null);
    });
    return Lute.New().VditorIRDOM2Md(copy.innerHTML);
  }

  function restoreSelection(ranges) {
    const selection = window.getSelection();
    if (!selection) return;
    selection.removeAllRanges();
    ranges.forEach(range => {
      try {
        selection.addRange(range);
      } catch {
        // A Vditor normalization can replace a selected block. Keep the editor usable.
      }
    });
  }

  function applyAlignmentCommand(alignment) {
    const selection = window.getSelection();
    const ranges = selection
      ? Array.from({ length: selection.rangeCount }, (_, index) => selection.getRangeAt(index).cloneRange())
      : [];
    const targets = getSelectedAlignmentTargets();
    if (!targets.length) return;
    targets.forEach(target => applyAlignmentToTarget(target, alignment));
    hydrateAlignments();
    editor.focus();
    restoreSelection(ranges);
    emitInput();
  }

  function restoreCaretInsideAlignment(target, caretNode, caretOffset) {
    const selection = window.getSelection();
    if (!selection || !target?.isConnected) return;
    const range = document.createRange();
    try {
      if (caretNode?.isConnected && caretNode !== target && target.contains(caretNode)) {
        const maximumOffset = caretNode.nodeType === Node.TEXT_NODE
          ? (caretNode.textContent || "").length
          : caretNode.childNodes.length;
        range.setStart(caretNode, Math.min(caretOffset, maximumOffset));
      } else {
        const parts = getAlignmentParts(target);
        if (!parts) return;
        range.setStartAfter(parts.opening);
      }
      range.collapse(true);
      selection.removeAllRanges();
      selection.addRange(range);
    } catch {
      // The editor remains usable even if Vditor replaces the caret node again.
    }
  }

  function inheritAlignmentAfterEnter(context) {
    const { previousTarget, alignment, targetCount } = context;
    const selection = window.getSelection();
    const caretNode = selection?.anchorNode;
    const caretOffset = selection?.anchorOffset || 0;
    const currentTarget = getAlignmentTarget(caretNode);
    const targets = getDocumentAlignmentTargets();
    const splitOccurred = targets.length > targetCount;
    const targetsToAlign = new Set();
    if (previousTarget?.isConnected) targetsToAlign.add(previousTarget);
    if (currentTarget) targetsToAlign.add(currentTarget);
    if (splitOccurred && currentTarget) {
      const currentIndex = targets.indexOf(currentTarget);
      if (currentIndex > 0) targetsToAlign.add(targets[currentIndex - 1]);
    }
    if (!targetsToAlign.size) return;
    targetsToAlign.forEach(target => applyAlignmentToTarget(target, alignment, true));
    hydrateAlignments();
    if (currentTarget) {
      restoreCaretInsideAlignment(currentTarget, caretNode, caretOffset);
    }
    emitInput();
  }

  function initialize(message) {
    resetPendingNestedListRetype();
    if (editor) editor.destroy();
    pendingNestedListRetype = false;
    backwardDeleteHandledInKeydown = false;
    lastEmptyNestedListItem = null;
    nestedDeleteRecovery = null;
    alignmentObserver?.disconnect();
    alignmentObserver = null;
    document.body.classList.toggle("swdt-compact", Boolean(message.compactLayout));
    document.body.classList.toggle("swdt-outline-collapsed", !message.outlineVisible);
    const toolbar = [
      "headings", "bold", "italic", "strike", "|", "quote", "list",
      "ordered-list", "check", "inline-code", "code", "link", "upload", "table",
      "|", "undo", "redo"
    ];
    editor = new Vditor("editor", {
      cdn: `${location.origin}/vditor`,
      mode: "ir",
      value: toEditorMarkdown(message.value || ""),
      placeholder: message.placeholder || "",
      lang: message.language || "en_US",
      theme: message.dark ? "dark" : "classic",
      icon: "ant",
      cache: { enable: false },
      toolbar,
      toolbarConfig: { pin: true },
      outline: { enable: !message.compactLayout, position: "left" },
      undoDelay: 750,
      preview: {
        markdown: {
          sanitize: true,
          footnotes: false,
          toc: false,
          codeBlockPreview: true,
          mathBlockPreview: false
        },
        theme: { current: message.dark ? "dark" : "light" },
        hljs: { enable: true, style: message.dark ? "github-dark" : "github" }
      },
      upload: {
        accept: "image/png,image/jpeg,image/gif,image/webp",
        handler: uploadFiles
      },
      input: emitInput,
      focus: () => post({ type: "focus", focused: true }),
      blur: () => {
        resetPendingNestedListRetype();
        clearTimeout(inputTimer);
        post({ type: "content", value: currentMarkdown() });
        post({ type: "focus", focused: false });
      },
      after: () => {
        const toolbarElement = document.querySelector(".vditor-toolbar");
        if (toolbarElement && !message.compactToolbar) {
          toolbarElement.classList.add("swdt-hidden-toolbar");
        }
        if (!message.compactLayout) {
          setTimeout(() => {
            if (!editor || !editor.vditor || !editor.vditor.outline) return;
            editor.vditor.outline.render(editor.vditor);
            bindOutlineNavigation(message.language);
            // Vditor hides outlines below its mobile breakpoint. SWDT owns the
            // surrounding split pane, so keep the outline available at compact widths.
            editor.vditor.outline.element.style.display = "block";
          }, 0);
        }
        setTimeout(observeAlignments, 0);
        post({ type: "initialized" });
      }
    });
  }

  function executeCommand(command) {
    resetPendingNestedListRetype();
    const alignment = {
      alignLeft: "left",
      alignCenter: "center",
      alignRight: "right"
    }[command];
    if (alignment) {
      applyAlignmentCommand(alignment);
      return;
    }
    const names = {
      heading: "headings",
      bold: "bold",
      italic: "italic",
      strike: "strike",
      quote: "quote",
      unorderedList: "list",
      orderedList: "ordered-list",
      taskList: "check",
      inlineCode: "inline-code",
      codeBlock: "code",
      link: "link",
      image: "upload",
      table: "table"
    };
    const name = names[command];
    const button = name ? document.querySelector(`.vditor-toolbar [data-type="${name}"]`) : null;
    if (button) button.click();
  }

  bridge.addEventListener("message", event => {
    const message = event.data || {};
    if (message.type === "initialize") {
      initialize(message);
    } else if (message.type === "setContent" && editor) {
      resetPendingNestedListRetype();
      suppressInput = true;
      editor.setValue(toEditorMarkdown(message.value || ""), true);
      suppressInput = false;
      scheduleAlignmentHydration();
    } else if (message.type === "flush") {
      clearTimeout(inputTimer);
      post({ type: "flushResult", requestId: message.requestId, value: currentMarkdown() });
    } else if (message.type === "command") {
      executeCommand(message.command);
    } else if (message.type === "insertAsset" && editor) {
      editor.insertValue(`![${message.fileName}](${toEditorMarkdown(message.uri)})`, true);
      emitInput();
    } else if (message.type === "theme" && editor) {
      editor.setTheme(message.dark ? "dark" : "classic", message.dark ? "dark" : "light", message.dark ? "github-dark" : "github");
    } else if (message.type === "outline") {
      document.body.classList.toggle("swdt-outline-collapsed", !message.visible);
    } else if (message.type === "focusEditor" && editor) {
      editor.focus();
    }
  });

  document.addEventListener("keydown", event => {
    const isBackspace = event.key === "Backspace" || event.code === "Backspace" || event.keyCode === 8;
    if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
      const previousTarget = getAlignmentTarget(window.getSelection()?.anchorNode);
      const alignment = previousTarget?.getAttribute("data-swdt-alignment");
      if (previousTarget && (alignment === "center" || alignment === "right")) {
        const context = {
          previousTarget,
          alignment,
          targetCount: getDocumentAlignmentTargets().length
        };
        setTimeout(() => requestAnimationFrame(() => inheritAlignmentAfterEnter(context)), 0);
      }
    }
    if (completePendingNestedListShortcut(event)) return;
    if (typePendingNestedListMarker(event)) {
      if (isBackspace) markBackwardDeleteHandledInKeydown();
      return;
    }
    if (isBackspace && !event.ctrlKey && !event.altKey && !event.metaKey && !event.shiftKey && handleBackwardDelete(event)) {
      markBackwardDeleteHandledInKeydown();
      return;
    }
    if (pendingNestedListRetype && !/^[\d.]$/.test(event.key)) {
      resetPendingNestedListRetype();
    }
    completePendingListShortcut(event);
    if (event.key === "Escape") {
      event.preventDefault();
      post({ type: "escape" });
      return;
    }
    if (!event.ctrlKey) return;
    const key = event.key.toLowerCase();
    if (key === "z" || key === "y" || key === "s") {
      event.preventDefault();
      event.stopPropagation();
      clearTimeout(inputTimer);
      post({ type: "content", value: currentMarkdown() });
      post({ type: key === "s" ? "save" : (key === "y" || event.shiftKey ? "redo" : "undo") });
    }
  }, true);

  document.addEventListener("selectionchange", () => {
    if (pendingNestedListRetype) return;
    const listItem = getSelectionListItem();
    const list = listItem && listItem.parentElement;
    const parentListItem = list && list.parentElement && list.parentElement.closest(".vditor-ir li");
    lastEmptyNestedListItem = listItem && parentListItem && isSemanticallyEmptyListItem(listItem)
      ? listItem
      : null;
  });

  document.addEventListener("beforeinput", event => {
    if (isBackwardDeleteInputType(event.inputType)) {
      if (backwardDeleteHandledInKeydown) {
        preventEditorEvent(event);
        return;
      }
      handleBackwardDelete(event);
      return;
    }

    if (!pendingNestedListRetype || event.inputType !== "insertText" || typeof event.data !== "string") return;
    if (completePendingNestedListShortcut(event, event.data)) return;
    if (typePendingNestedListMarker(event, event.data)) return;
    resetPendingNestedListRetype();
  }, true);

  document.addEventListener("input", event => {
    if (!nestedDeleteRecovery || !isBackwardDeleteInputType(event.inputType)) return;
    setTimeout(recoverNestedListAfterNativeDelete, 0);
  }, true);

  document.addEventListener("click", event => {
    const pendingItem = getPendingNestedListItem();
    if (pendingItem && event.target instanceof Node && !pendingItem.contains(event.target)) {
      resetPendingNestedListRetype();
    }
    const anchor = event.target.closest && event.target.closest("a[href]");
    if (!anchor) return;
    const href = anchor.getAttribute("href") || "";
    if (/^https?:\/\//i.test(href)) {
      event.preventDefault();
      post({ type: "openLink", uri: href });
    }
  }, true);

  post({ type: "ready" });
})();
