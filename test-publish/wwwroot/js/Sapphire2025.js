

//Función para mostrar un diálogo modal.
window.showModal = (modalId) => {
    var modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
};

//Función del foco por defecto.
window.focusElement = (element) => {
    if (element & typeof element.focus == "function") {
        element.focus();
    }
};

//Rutina para hacer click en un InputFile oculto desde un botón normal
window.triggerFileInputClick = function (element) {
    if (element) element.click();
}

//Rutina para pegar contenido del portapapeles en un elemento HTML.
window.clipboardInterop = {
    registerPasteListener: function (elemento, refDotNet) {
        elemento.addEventListener("paste", function (evento) {
            if (evento.clipboardData) {
                var html = evento.clipboardData.getData("text/html");
                refDotNet.invokeMethodAsync("receiveHtmlFromClipboard", html);
                evento.preventDefault();
            }
        })
    }
};

