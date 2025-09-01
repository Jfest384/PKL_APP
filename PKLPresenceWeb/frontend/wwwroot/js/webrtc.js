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
                    width: { ideal: 1280 },
                    height: { ideal: 720 }   // 1280x720 = 16:9, tapi akan dipaksa ke 4:3
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

        // Target rasio 4:3
        const targetRatio = 4 / 3;
        const videoRatio = video.videoWidth / video.videoHeight;

        let sx, sy, sw, sh;

        if (videoRatio > targetRatio) {
            // Video lebih lebar dari 4:3 → crop kiri & kanan
            sh = video.videoHeight;
            sw = sh * targetRatio;
            sx = (video.videoWidth - sw) / 2;
            sy = 0;
        } else {
            // Video lebih tinggi dari 4:3 → crop atas & bawah
            sw = video.videoWidth;
            sh = sw / targetRatio;
            sx = 0;
            sy = (video.videoHeight - sh) / 2;
        }

        // Ukuran awal hasil (misalnya max width 800px)
        let targetWidth = 800;
        let targetHeight = Math.floor(targetWidth * 3 / 4);

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        // Fungsi kompresi hingga <= 2MB
        async function compressToMax2MB() {
            canvas.width = targetWidth;
            canvas.height = targetHeight;
            ctx.drawImage(video, sx, sy, sw, sh, 0, 0, targetWidth, targetHeight);

            let quality = 0.9;
            let blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));

            // Turunkan kualitas hingga <= 2MB
            while (blob.size > 2 * 1024 * 1024 && quality > 0.1) {
                quality -= 0.1;
                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
            }

            // Jika masih lebih dari 2MB → perkecil resolusi bertahap
            while (blob.size > 2 * 1024 * 1024) {
                targetWidth = Math.floor(targetWidth * 0.9);
                targetHeight = Math.floor(targetWidth * 0.9 * 3 / 4); // tetap 4:3
                canvas.width = targetWidth;
                canvas.height = targetHeight;
                ctx.drawImage(video, sx, sy, sw, sh, 0, 0, targetWidth, targetHeight);
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
