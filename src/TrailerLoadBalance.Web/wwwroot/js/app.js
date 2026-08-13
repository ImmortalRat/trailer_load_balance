// Drag interaction + localStorage persistence/cross-tab sync for the trailer load balancer.
// Kept as one small vanilla-JS module (no build step) since this app has no other client tooling.
//
// The floor plan SVG uses viewBox units == inches, so Blazor never needs to know pixel scale;
// it just renders transform="translate(xIn yIn)". JS only needs pixels-per-inch (ppi) to convert
// raw pointer-event CSS-pixel deltas into inches while dragging.
//
// Everything drag-related uses Pointer Events (pointerdown/move/up), not native HTML5
// drag-and-drop - deliberately. The HTML5 DnD API (draggable="true", dragstart/dragover/drop) has
// real cross-browser gaps (Safari in particular does not reliably populate dataTransfer.types
// during dragover, which silently blocks the drop). Pointer Events are broadly and consistently
// supported and are already used for repositioning cargo on the canvas, so palette-to-canvas
// dragging reuses the exact same mechanism instead of a second, less reliable one.
window.tlb = (function () {
    const STORAGE_KEY = "tlb.state.v1";
    const DRAG_SEND_INTERVAL_MS = 50;
    const MOVE_THRESHOLD_IN_PX = 6;

    let dragState = null;
    let paletteDragState = null;
    let floorPlanDotNetRef = null;
    let floorPlanContainerEl = null;
    let pixelsPerInch = 1;
    let lastSentAt = 0;
    let totalLengthIn = 1;
    let viewBoxMinY = 0;
    let resizeObserver = null;
    let paletteDragBound = false;

    function initFloorPlan(containerId, dotNetHelper, lengthIn, widthIn) {
        floorPlanDotNetRef = dotNetHelper;
        totalLengthIn = lengthIn;
        viewBoxMinY = -widthIn / 2;

        const container = document.getElementById(containerId);
        if (!container) {
            return;
        }
        floorPlanContainerEl = container;

        recomputeScale(container);
        ensurePaletteDragBound();

        if (container.dataset.tlbBound) {
            return;
        }
        container.dataset.tlbBound = "1";

        container.addEventListener("pointerdown", onPointerDown);
        container.addEventListener("pointermove", onPointerMove);
        container.addEventListener("pointerup", onPointerUp);
        container.addEventListener("pointercancel", onPointerUp);

        resizeObserver = new ResizeObserver(() => recomputeScale(container));
        resizeObserver.observe(container);
    }

    function recomputeScale(container) {
        const width = container.getBoundingClientRect().width;
        if (width > 0 && totalLengthIn > 0) {
            pixelsPerInch = width / totalLengthIn;
        }
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    // ---- Repositioning cargo already on the floor plan ----

    function onPointerDown(e) {
        const target = e.target.closest("[data-cargo-id]");
        if (!target) {
            return;
        }
        target.setPointerCapture(e.pointerId);
        dragState = {
            cargoId: target.getAttribute("data-cargo-id"),
            pointerId: e.pointerId,
            startClientX: e.clientX,
            startClientY: e.clientY,
            startXIn: parseFloat(target.getAttribute("data-x-in")),
            startYIn: parseFloat(target.getAttribute("data-y-in")),
            minX: parseFloat(target.getAttribute("data-min-x-in")),
            maxX: parseFloat(target.getAttribute("data-max-x-in")),
            minY: parseFloat(target.getAttribute("data-min-y-in")),
            maxY: parseFloat(target.getAttribute("data-max-y-in")),
            el: target,
            moved: false,
        };
        target.classList.add("dragging");
        e.preventDefault();
    }

    function onPointerMove(e) {
        if (!dragState || dragState.pointerId !== e.pointerId) {
            return;
        }
        const dxIn = (e.clientX - dragState.startClientX) / pixelsPerInch;
        const dyIn = (e.clientY - dragState.startClientY) / pixelsPerInch;
        if (Math.abs(dxIn) > 0.05 || Math.abs(dyIn) > 0.05) {
            dragState.moved = true;
        }
        const newX = clamp(dragState.startXIn + dxIn, dragState.minX, dragState.maxX);
        const newY = clamp(dragState.startYIn + dyIn, dragState.minY, dragState.maxY);

        dragState.el.setAttribute("transform", `translate(${newX} ${newY})`);
        dragState.el.setAttribute("data-x-in", newX);
        dragState.el.setAttribute("data-y-in", newY);

        const now = performance.now();
        if (floorPlanDotNetRef && now - lastSentAt >= DRAG_SEND_INTERVAL_MS) {
            lastSentAt = now;
            floorPlanDotNetRef.invokeMethodAsync("OnCargoDragging", dragState.cargoId, newX, newY);
        }
    }

    function onPointerUp(e) {
        if (!dragState || dragState.pointerId !== e.pointerId) {
            return;
        }
        dragState.el.classList.remove("dragging");
        const cargoId = dragState.cargoId;
        const moved = dragState.moved;
        const x = parseFloat(dragState.el.getAttribute("data-x-in"));
        const y = parseFloat(dragState.el.getAttribute("data-y-in"));
        dragState = null;
        if (floorPlanDotNetRef) {
            floorPlanDotNetRef.invokeMethodAsync("OnCargoDragEnd", cargoId, x, y, moved);
        }
    }

    // ---- Dragging a palette item onto the floor plan to add new cargo ----
    // Bound on `document` (once) since palette items live outside the floor plan's own
    // container. A plain click (no movement past the threshold) is left alone entirely so
    // Blazor's normal @onclick "click to add" keeps working as a fallback - only once the
    // pointer has actually moved do we take over, show a drag ghost, and suppress the click.

    function ensurePaletteDragBound() {
        if (paletteDragBound) {
            return;
        }
        paletteDragBound = true;

        document.addEventListener("pointerdown", (e) => {
            const target = e.target.closest("[data-catalog-id]");
            if (!target) {
                return;
            }
            paletteDragState = {
                catalogId: target.getAttribute("data-catalog-id"),
                icon: target.getAttribute("data-catalog-icon") || "\u{1F4E6}",
                pointerId: e.pointerId,
                startClientX: e.clientX,
                startClientY: e.clientY,
                moved: false,
                ghostEl: null,
            };
        });

        document.addEventListener("pointermove", (e) => {
            if (!paletteDragState || paletteDragState.pointerId !== e.pointerId) {
                return;
            }
            const dx = e.clientX - paletteDragState.startClientX;
            const dy = e.clientY - paletteDragState.startClientY;
            if (!paletteDragState.moved && Math.hypot(dx, dy) > MOVE_THRESHOLD_IN_PX) {
                paletteDragState.moved = true;
                paletteDragState.ghostEl = createPaletteGhost(paletteDragState.icon);
            }
            if (paletteDragState.moved && paletteDragState.ghostEl) {
                paletteDragState.ghostEl.style.left = `${e.clientX}px`;
                paletteDragState.ghostEl.style.top = `${e.clientY}px`;
                if (floorPlanContainerEl) {
                    const over = isPointOverElement(e.clientX, e.clientY, floorPlanContainerEl);
                    floorPlanContainerEl.classList.toggle("drop-target-active", over);
                }
            }
        });

        const end = (e) => {
            if (!paletteDragState || paletteDragState.pointerId !== e.pointerId) {
                return;
            }
            const drag = paletteDragState;
            paletteDragState = null;

            if (drag.ghostEl) {
                drag.ghostEl.remove();
            }
            if (floorPlanContainerEl) {
                floorPlanContainerEl.classList.remove("drop-target-active");
            }
            if (!drag.moved) {
                // Plain click/tap - let the normal Blazor onclick handle adding the item.
                return;
            }

            if (floorPlanContainerEl && floorPlanDotNetRef && isPointOverElement(e.clientX, e.clientY, floorPlanContainerEl)) {
                const rect = floorPlanContainerEl.getBoundingClientRect();
                const xIn = (e.clientX - rect.left) / pixelsPerInch;
                const yIn = (e.clientY - rect.top) / pixelsPerInch + viewBoxMinY;
                floorPlanDotNetRef.invokeMethodAsync("OnCatalogDrop", drag.catalogId, xIn, yIn);
            }
        };
        document.addEventListener("pointerup", end);
        document.addEventListener("pointercancel", end);
    }

    function isPointOverElement(clientX, clientY, el) {
        const rect = el.getBoundingClientRect();
        return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
    }

    function createPaletteGhost(icon) {
        const el = document.createElement("div");
        el.className = "tlb-drag-ghost";
        el.textContent = icon;
        document.body.appendChild(el);
        return el;
    }

    // localStorage can throw (not just return null) in private/incognito modes, or when a
    // browser's privacy settings block storage entirely - swallow that so the rest of the app
    // (which doesn't need persistence to function) keeps working instead of tearing down the
    // whole Blazor circuit on an unhandled JS interop exception.
    function saveState(json) {
        try {
            localStorage.setItem(STORAGE_KEY, json);
        } catch (err) {
            console.warn("tlb: could not save state to localStorage", err);
        }
    }

    function loadState() {
        try {
            return localStorage.getItem(STORAGE_KEY);
        } catch (err) {
            console.warn("tlb: could not read state from localStorage", err);
            return null;
        }
    }

    function registerSync(dotNetHelper) {
        try {
            window.addEventListener("storage", (e) => {
                if (e.key === STORAGE_KEY) {
                    dotNetHelper.invokeMethodAsync("OnExternalStateChanged");
                }
            });
        } catch (err) {
            console.warn("tlb: could not register storage listener", err);
        }
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible") {
                dotNetHelper.invokeMethodAsync("OnExternalStateChanged");
            }
        });
    }

    return { initFloorPlan, saveState, loadState, registerSync };
})();
