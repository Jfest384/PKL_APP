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
            // Langkah 1: Minta izin kamera terlebih dahulu (agar label bisa terbaca)
            await navigator.mediaDevices.getUserMedia({ video: true });

            // Langkah 2: Enumerasi semua kamera video
            const devices = await navigator.mediaDevices.enumerateDevices();
            const videoDevices = devices.filter(device => device.kind === 'videoinput');

            // Langkah 3: Cari kamera belakang utama (hindari 0.5x jika bisa)
            let selectedDevice = videoDevices.find(device =>
                device.label.toLowerCase().includes("back") &&
                !device.label.toLowerCase().includes("0.5")
            );

            // Jika tidak ditemukan, fallback ke kamera pertama (biasanya webcam depan)
            if (!selectedDevice) {
                selectedDevice = videoDevices[0];
            }

            // Langkah 4: Minta akses kamera berdasarkan deviceId
            const constraints = {
                video: { deviceId: selectedDevice.deviceId }
            };

            const stream = await navigator.mediaDevices.getUserMedia(constraints);

            video.srcObject = stream;
            window.webrtcPhoto.streams[elementId] = stream;

            // Klik video juga bisa capture
            video.onclick = () => window.webrtcPhoto.capture(elementId);
            video.tabIndex = 0;
        } catch (e) {
            alert("tidak bisa mengakses kamera");
            console.error(e);
        }
    },

    capture(elementId) {
        const video = document.getElementById(elementId);
        if (!video) return;

        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Ambil data base64 JPG
        const dataUrl = canvas.toDataURL('image/jpeg');

        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnWebRTCCapture', elementId, dataUrl);
        } else {
            alert("dotNetRef tidak ditemukan!");
        }

        // Hentikan kamera
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