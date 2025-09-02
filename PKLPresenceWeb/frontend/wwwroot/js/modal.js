window.modalEscHelper = {
    registerEsc: function (dotNetObjRef) {
        function handler(e) {
            if (e.key === "Escape") {
                dotNetObjRef.invokeMethodAsync("OnEscPressed");
            }
        }
        window.addEventListener("keydown", handler);
        dotNetObjRef._escHandler = handler;
    },
    unregisterEsc: function (dotNetObjRef) {
        if (dotNetObjRef._escHandler) {
            window.removeEventListener("keydown", dotNetObjRef._escHandler);
            dotNetObjRef._escHandler = null;
        }
    }
};