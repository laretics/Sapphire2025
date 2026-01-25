///Se usa para dar funcionalidad al splitter del panel de navegación izquierda
export function initializeSplitter(splitterId, leftPanelId) {
    let isResizing = false;
    const splitter = document.getElementById('splitter');
    const leftPanel = document.getElementById('leftPanel');
    const container = leftPanel?.parentElement;

    if (splitter && leftPanel && container) {
        splitter.addEventListener('mousedown', function (e) {
            isResizing = true;
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
        });

        document.addEventListener('mousemove', function (e) {
            if (!isResizing) return;

            // Obtener el offset del contenedor relativo al viewport
            const containerRect = container.getBoundingClientRect();
            const relativeX = e.clientX - containerRect.left;
            const containerWidth = containerRect.width;
            const newWidth = (relativeX / containerWidth) * 100;

            // Limitar entre 15% y 50%
            if (newWidth >= 15 && newWidth <= 50) {
                leftPanel.style.flex = 'none';
                leftPanel.style.width = newWidth + '%';
            }
        });

        document.addEventListener('mouseup', function (e) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
        });
    }
}

export function getElementDimensions(element) {
    if (!element) return { width: 0, height: 0 };
    const rect = element.getBoundingClientRect();
    return {
        width: Math.floor(rect.width),
        height: Math.floor(rect.height)
    }
}