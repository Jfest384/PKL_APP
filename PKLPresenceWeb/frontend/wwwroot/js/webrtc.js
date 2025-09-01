window.webrtcPhoto = {
    streams: {},
    dotNetRefs: {},

    async start(elementId, dotNetRef) {
        const video = document.getElementById(elementId);
        if (!video) return;

        // Hentikan stream sebelumnya jika ada
        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }

        window.webrtcPhoto.dotNetRefs[elementId] = dotNetRef;

        try {
            const constraints = {
                video: {
                    facingMode: "environment",
                    width: { ideal: 1280 },  // biarkan tetap HD
                    height: { ideal: 720 }
                }
            };
            const stream = await navigator.mediaDevices.getUserMedia(constraints);

            video.srcObject = stream;
            window.webrtcPhoto.streams[elementId] = stream;

            video.onclick = () => window.webrtcPhoto.capture(elementId);
            video.tabIndex = 0;
        } catch (e) {
            alert("tidak bisa mengakses kamera");
        }
    },

    async capture(elementId) {
        const video = document.getElementById(elementId);
        if (!video) return;

        // 🔹 Atur resolusi awal (misalnya maksimal 800px lebar)
        const maxWidth = 800;
        const scale = maxWidth / video.videoWidth;
        let targetWidth = Math.min(video.videoWidth, maxWidth);
        let targetHeight = video.videoHeight * scale;

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        // Fungsi kompresi hingga <= 2MB
        async function compressToMax2MB() {
            canvas.width = targetWidth;
            canvas.height = targetHeight;
            ctx.drawImage(video, 0, 0, targetWidth, targetHeight);

            let quality = 0.9;
            let blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));

            // Turunkan kualitas hingga <= 2MB atau minimal quality 0.1
            while (blob.size > 2 * 1024 * 1024 && quality > 0.1) {
                quality -= 0.1;
                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
            }

            // Jika masih lebih dari 2MB, kurangi resolusi bertahap
            while (blob.size > 2 * 1024 * 1024) {
                targetWidth = Math.floor(targetWidth * 0.9);
                targetHeight = Math.floor(targetHeight * 0.9);
                canvas.width = targetWidth;
                canvas.height = targetHeight;
                ctx.drawImage(video, 0, 0, targetWidth, targetHeight);
                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
            }

            return blob;
        }

        const blob = await compressToMax2MB();

        // Convert blob ke base64
        const dataUrl = await new Promise(resolve => {
            const reader = new FileReader();
            reader.onloadend = () => resolve(reader.result);
            reader.readAsDataURL(blob);
        });

        // Kirim hasil ke Blazor
        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnWebRTCCapture', elementId, dataUrl);
        } else {
            alert("dotNetRef tidak ditemukan!");
        }

        // Hentikan kamera setelah capture
        window.webrtcPhoto.stop(elementId);
        delete window.webrtcPhoto.dotNetRefs[elementId];
    },

    stop(elementId) {
        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }
    }
};
