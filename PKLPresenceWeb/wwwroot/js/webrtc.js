//window.webrtcPhoto = {
//    streams: {},
//    dotNetRefs: {},
//    timers: {},
//    facingModes: {},

//    async start(elementId, dotNetRef, facingMode = "environment") {
//        const video = document.getElementById(elementId);
//        if (!video) return;

//        if (window.webrtcPhoto.streams[elementId]) {
//            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
//            delete window.webrtcPhoto.streams[elementId];
//        }

//        window.webrtcPhoto.dotNetRefs[elementId] = dotNetRef;
//        window.webrtcPhoto.facingModes[elementId] = facingMode;

//        try {
//            const constraints = {
//                video: {
//                    facingMode: facingMode,
//                    width: { ideal: 800 },
//                    height: { ideal: 600 }
//                }
//            };
//            const stream = await navigator.mediaDevices.getUserMedia(constraints);

//            video.srcObject = stream;
//            window.webrtcPhoto.streams[elementId] = stream;
//            video.tabIndex = 0;
//        } catch (e) {
//            alert("tidak bisa mengakses kamera: " + e.message);
//            console.error(e);
//        }
//    },

//    SetTimer(elementId, seconds) {
//        window.webrtcPhoto.timers[elementId] = seconds;
//    },

//    async capture(elementId) {
//        const video = document.getElementById(elementId);
//        if (!video) return;

//        const delay = window.webrtcPhoto.timers[elementId] || 0;
//        if (delay > 0) {
//            const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
//            for (let i = delay; i > 0; i--) {
//                if (dotNetRef) {
//                    dotNetRef.invokeMethodAsync('UpdateWebRTCCaptureCountdown', i);
//                }
//                await new Promise(resolve => setTimeout(resolve, 1000));
//            }
//            if (dotNetRef) {
//                dotNetRef.invokeMethodAsync('UpdateWebRTCCaptureCountdown', 0);
//            }
//            await new Promise(resolve => setTimeout(resolve, 350));
//        }

//        if (video.readyState < 2 || video.videoWidth === 0 || video.videoHeight === 0) {
//            alert("Video belum siap, coba lagi.");
//            return;
//        }

//        const sw = video.videoWidth;
//        const sh = video.videoHeight;

//        const canvas = document.createElement('canvas');
//        const ctx = canvas.getContext('2d');
//        canvas.width = sw;
//        canvas.height = sh;

//        ctx.drawImage(video, 0, 0, sw, sh);

//        async function compressToMax2MB() {
//            let quality = 0.9;
//            let blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));

//            while (blob && blob.size > 2 * 1024 * 1024 && quality > 0.1) {
//                quality -= 0.1;
//                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
//            }

//            while (blob && blob.size > 2 * 1024 * 1024) {
//                canvas.width = Math.floor(canvas.width * 0.9);
//                canvas.height = Math.floor(canvas.height * 0.9);
//                ctx.drawImage(video, 0, 0, sw, sh, 0, 0, canvas.width, canvas.height);
//                blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", quality));
//            }

//            return blob;
//        }

//        const blob = await compressToMax2MB();
//        if (!blob) {
//            alert("Gagal menangkap gambar.");
//            return;
//        }

//        const dataUrl = await new Promise(resolve => {
//            const reader = new FileReader();
//            reader.onloadend = () => resolve(reader.result);
//            reader.readAsDataURL(blob);
//        });

//        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];
//        if (dotNetRef) {
//            dotNetRef.invokeMethodAsync('OnWebRTCCapture', elementId, dataUrl);
//        } else {
//            alert("dotNetRef tidak ditemukan!");
//        }
//    },

//    async switchCamera(elementId) {
//        const devices = await navigator.mediaDevices.enumerateDevices();
//        const videoInputs = devices.filter(d => d.kind === 'videoinput');
//        if (videoInputs.length < 2) {
//            alert("Perangkat Anda hanya memiliki satu kamera. Tidak bisa switch kamera.");
//            return;
//        }

//        const current = window.webrtcPhoto.facingModes[elementId] || "environment";
//        const newMode = current === "environment" ? "user" : "environment";

//        console.log(`🔄 Switch camera: ${current} → ${newMode}`);
//        const dotNetRef = window.webrtcPhoto.dotNetRefs[elementId];

//        await window.webrtcPhoto.start(elementId, dotNetRef, newMode);
//    },

//    stop(elementId) {
//        if (window.webrtcPhoto.streams[elementId]) {
//            window.webrtcPhoto.streams[elementId].getTracks().forEach(track => track.stop());
//            delete window.webrtcPhoto.streams[elementId];
//        }
//    }
//};


//window.webrtcPhoto = {
//    streams: {},
//    dotNetRefs: {},
//    timers: {},
//    currentDeviceIds: {},
//    cameraList: {},

//    // -----------------------------
//    // Ambil daftar kamera
//    // -----------------------------
//    async initCameraList() {
//        const devices = await navigator.mediaDevices.enumerateDevices();
//        const cams = devices.filter(d => d.kind === "videoinput");
//        return cams.map(c => ({ deviceId: c.deviceId, label: c.label }));
//    },

//    // -----------------------------
//    // Start kamera (default belakang)
//    // -----------------------------
//    async start(elementId, dotNetRef, preferred = "environment") {
//        const video = document.getElementById(elementId);
//        if (!video) return;

//        // Stop stream lama
//        if (this.streams[elementId]) {
//            this.streams[elementId].getTracks().forEach(t => t.stop());
//        }

//        this.dotNetRefs[elementId] = dotNetRef;

//        const cams = await this.initCameraList();
//        this.cameraList[elementId] = cams;

//        if (cams.length === 0) {
//            alert("Tidak ada kamera ditemukan.");
//            return;
//        }

//        // Tentukan kamera default
//        let selectedCam;
//        if (preferred === "environment") {
//            selectedCam = cams.find(c => c.label.toLowerCase().includes("back")) || cams[0];
//        } else {
//            selectedCam = cams.find(c => c.label.toLowerCase().includes("front")) || cams[0];
//        }

//        this.currentDeviceIds[elementId] = selectedCam.deviceId;

//        try {
//            const stream = await navigator.mediaDevices.getUserMedia({
//                video: { deviceId: selectedCam.deviceId }
//            });

//            video.srcObject = stream;
//            this.streams[elementId] = stream;

//        } catch (e) {
//            alert("Gagal membuka kamera: " + e.message);
//            console.error(e);
//        }
//    },

//    // -----------------------------
//    // Timer (detik)
//    // -----------------------------
//    SetTimer(elementId, seconds) {
//        this.timers[elementId] = seconds;
//    },

//    // -----------------------------
//    // Capture with compression
//    // -----------------------------
//    async capture(elementId) {
//        const video = document.getElementById(elementId);
//        if (!video) return;

//        // ⏳ Timer countdown
//        const delay = this.timers[elementId] || 0;
//        if (delay > 0) {
//            const dotNetRef = this.dotNetRefs[elementId];
//            for (let i = delay; i > 0; i--) {
//                dotNetRef?.invokeMethodAsync('UpdateWebRTCCaptureCountdown', i);
//                await new Promise(resolve => setTimeout(resolve, 1000));
//            }
//            dotNetRef?.invokeMethodAsync('UpdateWebRTCCaptureCountdown', 0);
//            await new Promise(resolve => setTimeout(resolve, 300));
//        }

//        // Pastikan video siap
//        if (video.readyState < 2 || video.videoWidth === 0) {
//            alert("Video belum siap.");
//            return;
//        }

//        const sw = video.videoWidth;
//        const sh = video.videoHeight;

//        const canvas = document.createElement('canvas');
//        const ctx = canvas.getContext('2d');
//        canvas.width = sw;
//        canvas.height = sh;

//        ctx.drawImage(video, 0, 0, sw, sh);

//        // Kompres 2MB
//        async function compress() {
//            let quality = 0.9;
//            let blob = await new Promise(r => canvas.toBlob(r, "image/jpeg", quality));

//            while (blob && blob.size > 2 * 1024 * 1024 && quality > 0.1) {
//                quality -= 0.1;
//                blob = await new Promise(r => canvas.toBlob(r, "image/jpeg", quality));
//            }

//            return blob;
//        }

//        const blob = await compress();
//        if (!blob) {
//            alert("Gagal menangkap gambar.");
//            return;
//        }

//        // Convert ke base64
//        const dataUrl = await new Promise(resolve => {
//            const reader = new FileReader();
//            reader.onloadend = () => resolve(reader.result);
//            reader.readAsDataURL(blob);
//        });

//        const dotNetRef = this.dotNetRefs[elementId];
//        dotNetRef?.invokeMethodAsync("OnWebRTCCapture", elementId, dataUrl);
//    },

//    // -----------------------------
//    // Switch camera (deviceId-based)
//    // -----------------------------
//    async switchCamera(elementId) {
//        const cams = this.cameraList[elementId] || [];

//        if (cams.length < 2) {
//            alert("Perangkat hanya punya 1 kamera.");
//            return;
//        }

//        const currentId = this.currentDeviceIds[elementId];
//        const index = cams.findIndex(c => c.deviceId === currentId);
//        const nextIndex = (index + 1) % cams.length;
//        const nextCam = cams[nextIndex];

//        this.currentDeviceIds[elementId] = nextCam.deviceId;

//        // Stop stream lama
//        if (this.streams[elementId]) {
//            this.streams[elementId].getTracks().forEach(t => t.stop());
//        }

//        try {
//            const stream = await navigator.mediaDevices.getUserMedia({
//                video: { deviceId: nextCam.deviceId }
//            });

//            const video = document.getElementById(elementId);
//            video.srcObject = stream;
//            this.streams[elementId] = stream;

//        } catch (e) {
//            alert("Gagal switch kamera: " + e.message);
//            console.error(e);
//        }
//    },

//    // -----------------------------
//    // Stop
//    // -----------------------------
//    stop(elementId) {
//        if (this.streams[elementId]) {
//            this.streams[elementId].getTracks().forEach(t => t.stop());
//            delete this.streams[elementId];
//        }
//    }
//};

window.webrtcPhoto = {
    streams: {},
    dotNetRefs: {},
    timers: {},
    deviceMap: {},

    isMobile() {
        return /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);
    },

    async enumerateCameras() {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices.filter(d => d.kind === "videoinput");
    },

    async start(elementId, dotNetRef, preferred = "environment") {
        const video = document.getElementById(elementId);
        if (!video) return;

        // stop stream lama
        if (this.streams[elementId]) {
            this.streams[elementId].getTracks().forEach(t => t.stop());
            delete this.streams[elementId];
        }

        const cameras = await this.enumerateCameras();

        if (cameras.length === 0) {
            alert("Tidak ada kamera ditemukan.");
            return;
        }

        this.dotNetRefs[elementId] = dotNetRef;

        let constraints = { video: {} };

        // === MOBILE MODE (facingMode) ===
        if (this.isMobile()) {
            constraints.video = {
                facingMode: preferred === "environment" ? { exact: "environment" } : "user"
            };
        }
        // === DESKTOP MODE (deviceId exact) ===
        else {
            if (!this.deviceMap[elementId]) {
                // default: pilih camera belakang jika ada (nama biasanya mengandung 'back' atau 'rear')
                const backCam = cameras.find(c => /back|rear|environment/i.test(c.label));
                this.deviceMap[elementId] = backCam?.deviceId || cameras[0].deviceId;
            }

            constraints.video.deviceId = { exact: this.deviceMap[elementId] };
        }

        console.log("▶ Starting camera with constraints:", constraints);

        try {
            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            video.srcObject = stream;
            this.streams[elementId] = stream;
        } catch (err) {
            console.error("Camera error:", err);

            alert("Gagal membuka kamera: " + err.message);
        }
    },

    async switchCamera(elementId) {
        const cameras = await this.enumerateCameras();

        // Jika hanya 1 kamera → jangan switch
        if (cameras.length < 2) {
            alert("Perangkat hanya memiliki 1 kamera.");
            return;
        }

        // === MOBILE: gunakan facingMode ===
        if (this.isMobile()) {
            let video = this.streams[elementId]?.getVideoTracks()[0];
            let currentFacing = video?.getSettings().facingMode || "environment";

            let nextFacing = currentFacing === "environment" ? "user" : "environment";
            console.log("🔄 Mobile switch:", currentFacing, "→", nextFacing);

            return await this.start(elementId, this.dotNetRefs[elementId], nextFacing);
        }

        // === DESKTOP: gunakan deviceId ===
        let currentDeviceId = this.deviceMap[elementId];
        let idx = cameras.findIndex(c => c.deviceId === currentDeviceId);

        let nextIdx = (idx + 1) % cameras.length;
        this.deviceMap[elementId] = cameras[nextIdx].deviceId;

        console.log("🔄 Desktop switch →", cameras[nextIdx].label);

        return await this.start(elementId, this.dotNetRefs[elementId], "environment");
    },

    SetTimer(elementId, seconds) {
        this.timers[elementId] = seconds;
    },

    async capture(elementId) {
        const video = document.getElementById(elementId);
        if (!video) return;

        const delay = this.timers[elementId] || 0;

        if (delay > 0) {
            let dot = this.dotNetRefs[elementId];
            for (let i = delay; i > 0; i--) {
                dot?.invokeMethodAsync('UpdateWebRTCCaptureCountdown', i);
                await new Promise(r => setTimeout(r, 1000));
            }
            dot?.invokeMethodAsync('UpdateWebRTCCaptureCountdown', 0);
            await new Promise(r => setTimeout(r, 350));
        }

        // cek siap
        if (video.readyState < 2) {
            alert("Video belum siap.");
            return;
        }

        const sw = video.videoWidth;
        const sh = video.videoHeight;

        const canvas = document.createElement("canvas");
        const ctx = canvas.getContext("2d");

        canvas.width = sw;
        canvas.height = sh;

        ctx.drawImage(video, 0, 0, sw, sh);

        const blob = await new Promise(resolve =>
            canvas.toBlob(resolve, "image/jpeg", 0.9)
        );

        const reader = new FileReader();
        reader.onloadend = () => {
            const dataUrl = reader.result;
            this.dotNetRefs[elementId]?.invokeMethodAsync("OnWebRTCCapture", elementId, dataUrl);
        };
        reader.readAsDataURL(blob);
    },

    stop(elementId) {
        if (this.streams[elementId]) {
            this.streams[elementId].getTracks().forEach(t => t.stop());
            delete this.streams[elementId];
        }
    }
};
