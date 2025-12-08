window.fetchWithCookie = async function (url, options) {
    options = options || {};
    options.credentials = 'include';

    const response = await fetch(url, options);
    const text = await response.text();

    return {
        status: response.status,
        ok: response.ok,
        text: text
    };
}