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

window.clipboardInterop = {
    gethtmlfromclipboard: function (dotnetHelper) {
        document.addEventListener('paste', function (e) {
            for (const item of e.clipboardData.items) {
                if (item.type == 'text/html') {
                    item.getasString(function (html) {
                        dotnetHelper.invokeMethodAsync('ReceiveHtmlFromClipboard', html);
                    });
                    e.preventDefault();
                    break; //No necesito recuperar más elementos.
                }
            }
        });
    }
};