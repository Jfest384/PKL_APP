window.webrtcPhoto = {
    streams: {},
    dotNetRefs: {},
    facingModes: {}, // Tambahan: simpan mode kamera per elementId
    start: async function (elementId, dotNetRef, facingMode) {
        const video = document.getElementById(elementId);
        if (!video) return;
        window.webrtcPhoto.dotNetRefs[elementId] = dotNetRef;
        // Default facingMode: user (depan)
        facingMode = facingMode || window.webrtcPhoto.facingModes[elementId] || "user";
        window.webrtcPhoto.facingModes[elementId] = facingMode;

        // Stop stream jika ada
        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }

        try {
            // GUNAKAN tanpa exact!
            const stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: facingMode }
            });
            video.srcObject = stream;
            window.webrtcPhoto.streams[elementId] = stream;

            video.onkeydown = (e) => {
                if (e.key === "Enter" || e.key === " " || e.code === "Space") {
                    window.webrtcPhoto.capture(elementId);
                }
            };
            video.tabIndex = 0;
            setTimeout(() => video.focus(), 100);
        } catch (e) {
            // Fallback ke kamera default jika gagal
            if (facingMode !== "user") {
                try {
                    const stream = await navigator.mediaDevices.getUserMedia({ video: true });
                    video.srcObject = stream;
                    window.webrtcPhoto.streams[elementId] = stream;
                    window.webrtcPhoto.facingModes[elementId] = "user";
                } catch (err) {
                    alert("Tidak bisa mengakses kamera.");
                }
            } else {
                alert("Tidak bisa mengakses kamera.");
            }
        }
    },
    switchCamera: async function (elementId) {
        // Toggle facingMode
        let current = window.webrtcPhoto.facingModes[elementId] || "user";
        let next = current === "user" ? "environment" : "user";
        window.webrtcPhoto.facingModes[elementId] = next;
        // Restart stream dengan facingMode baru
        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
        await window.webrtcPhoto.start(elementId, dotNetRef, next);
    },
    capture: function (elementId) {
        const video = document.getElementById(elementId);
        if (!video) return;
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        const dataUrl = canvas.toDataURL('image/jpeg');
        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnWebRTCCapture', elementId, dataUrl);
        }
        window.webrtcPhoto.stop(elementId);
    },
    stop: function (elementId) {
        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }
        if (window.webrtcPhoto.dotNetRefs[elementId]) {
            delete window.webrtcPhoto.dotNetRefs[elementId];
        }
        if (window.webrtcPhoto.facingModes[elementId]) {
            delete window.webrtcPhoto.facingModes[elementId];
        }
        const video = document.getElementById(elementId);
        if (video) {
            video.srcObject = null;
        }
    }
};