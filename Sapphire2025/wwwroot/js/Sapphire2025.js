//Función para mostrar un diálogo modal.
window.showModal = (modalId) => {
    var modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
};

//Función del foco por defecto.
window.focusElement = (element) => {
    if (element instanceof HTMLElement) {
        element.focus();
    } else if (element && element instanceof Object && 'focus' in element) {
        // Para compatibilidad con Blazor Server/wasm
        element.focus();
    } else if (element) {
        // Intenta obtener el elemento por id si es posible
        let el = element instanceof Object && 'id' in element ? document.getElementById(element.id) : null;
        if (el) el.focus();
    }
};