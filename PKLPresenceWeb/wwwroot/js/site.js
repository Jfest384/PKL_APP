window.downloadFileFromBytes = (fileName, contentType, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(link.href);
};

window.photoCompressor = {
    compressImageBase64: async function (base64, quality = 0.85) {
        return new Promise((resolve, reject) => {
            try {
                const img = new Image();

                img.onload = () => {
                    const canvas = document.createElement("canvas");
                    const ctx = canvas.getContext("2d");

                    canvas.width = img.width;
                    canvas.height = img.height;

                    ctx.drawImage(img, 0, 0);

                    const output = canvas.toDataURL("image/jpeg", quality);
                    resolve(output);
                };

                img.onerror = reject;

                img.src = "data:image/jpeg;base64," + base64;
            } catch (err) {
                reject(err);
            }
        });
    }
};
