window.webrtcPhoto = {
    streams: {},
    dotNetRefs: {},
    timers: {},
    facingModes: {},

    async start(elementId, dotNetRef, facingMode = "environment") {
        const video = document.getElementById(elementId);
        if (!video) return;

        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }

        window.webrtcPhoto.dotNetRefs[elementId] = dotNetRef;
        window.webrtcPhoto.facingModes[elementId] = facingMode;

        try {
            const constraints = {
                video: {
                    facingMode: facingMode,
                    width: { ideal: 800 },
                    height: { ideal: 600 }
                }
            };
            const stream = await navigator.mediaDevices.getUserMedia(constraints);

            video.srcObject = stream;
            window.webrtcPhoto.streams[elementId] = stream;
            video.tabIndex = 0;
        } catch (e) {
            alert("tidak bisa mengakses kamera: " + e.message);
            console.error(e);
        }
    },

    SetTimer(elementId, seconds) {
        window.webrtcPhoto.timers[elementId] = seconds;
    },

    async capture(elementId) {
        const video = document.getElementById(elementId);
        if (!video) return;

        const delay = window.webrtcPhoto.timers[elementId] || 0;
        if (delay > 0) {
            const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
            for (let i = delay; i > 0; i--) {
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('UpdateWebRTCCaptureCountdown', i);
                }
                await new Promise(resolve => setTimeout(resolve, 1000));
            }
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('UpdateWebRTCCaptureCountdown', 0);
            }
            await new Promise(resolve => setTimeout(resolve, 350));
        }

        if (video.readyState < 2 || video.videoWidth === 0 || video.videoHeight === 0) {
            alert("Video belum siap, coba lagi.");
            return;
        }

        const sw = video.videoWidth;
        const sh = video.videoHeight;

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        canvas.width = sw;
        canvas.height = sh;

        ctx.drawImage(video, 0, 0, sw, sh);

        async function compressToMax2MB() {
            let quality = 0.9;
            let blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));

            while (blob && blob.size > 2 * 1024 * 1024 && quality > 0.1) {
                quality -= 0.1;
                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
            }

            while (blob && blob.size > 2 * 1024 * 1024) {
                canvas.width = Math.floor(canvas.width * 0.9);
                canvas.height = Math.floor(canvas.height * 0.9);
                ctx.drawImage(video, 0, 0, sw, sh, 0, 0, canvas.width, canvas.height);
                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
            }

            return blob;
        }

        const blob = await compressToMax2MB();
        if (!blob) {
            alert("Gagal menangkap gambar.");
            return;
        }

        const dataUrl = await new Promise(resolve => {
            const reader = new FileReader();
            reader.onloadend = () => resolve(reader.result);
            reader.readAsDataURL(blob);
        });

        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnWebRTCCapture', elementId, dataUrl);
        } else {
            alert("dotNetRef tidak ditemukan!");
        }
    },

    async switchCamera(elementId) {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoInputs = devices.filter(d => d.kind === 'videoinput');
        if (videoInputs.length < 2) {
            alert("Perangkat Anda hanya memiliki satu kamera. Tidak bisa switch kamera.");
            return;
        }

        const current = window.webrtcPhoto.facingModes[elementId] || "environment";
        const newMode = current === "environment" ? "user" : "environment";

        console.log(`🔄 Switch camera: ${current} → ${newMode}`);
        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];

        await window.webrtcPhoto.start(elementId, dotNetRef, newMode);
    },

    stop(elementId) {
        if (window.webrtcPhoto.streams[elementId]) {
            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
            delete window.webrtcPhoto.streams[elementId];
        }
    }
};
