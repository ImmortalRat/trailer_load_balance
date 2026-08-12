// Drag interaction + localStorage persistence/cross-tab sync for the trailer load balancer.
// Kept as one small vanilla-JS module (no build step) since this app has no other client tooling.
//
// The floor plan SVG uses viewBox units == inches, so Blazor never needs to know pixel scale;
// it just renders transform="translate(xIn yIn)". JS only needs pixels-per-inch (ppi) to convert
// raw pointer-event CSS-pixel deltas into inches while dragging.
window.tlb = (function () {
    const STORAGE_KEY = "tlb.state.v1";
    const DRAG_SEND_INTERVAL_MS = 50;

    let dragState = null;
    let floorPlanDotNetRef = null;
    let pixelsPerInch = 1;
    let lastSentAt = 0;
    let totalLengthIn = 1;
    let resizeObserver = null;

    function initFloorPlan(containerId, dotNetHelper, lengthIn) {
        floorPlanDotNetRef = dotNetHelper;
        totalLengthIn = lengthIn;

        const container = document.getElementById(containerId);
        if (!container) {
            return;
        }

        recomputeScale(container);

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

    function saveState(json) {
        localStorage.setItem(STORAGE_KEY, json);
    }

    function loadState() {
        return localStorage.getItem(STORAGE_KEY);
    }

    function registerSync(dotNetHelper) {
        window.addEventListener("storage", (e) => {
            if (e.key === STORAGE_KEY) {
                dotNetHelper.invokeMethodAsync("OnExternalStateChanged");
            }
        });
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible") {
                dotNetHelper.invokeMethodAsync("OnExternalStateChanged");
            }
        });
    }

    return { initFloorPlan, saveState, loadState, registerSync };
})();
