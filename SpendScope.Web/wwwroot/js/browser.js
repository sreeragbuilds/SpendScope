// Small browser helpers used through JS interop.
window.spendscopeBrowser = {
  // sessionStorage holds the refresh token in v1 (spec 13.7). Wrapped in try/catch because storage
  // can be unavailable (private mode, blocked site data).
  sessionGet(key) { try { return sessionStorage.getItem(key); } catch { return null; } },
  sessionSet(key, value) { try { sessionStorage.setItem(key, value); } catch { } },
  sessionRemove(key) { try { sessionStorage.removeItem(key); } catch { } },

  // localStorage holds only the theme preference, never auth data (spec 19.10).
  localGet(key) { try { return localStorage.getItem(key); } catch { return null; } },
  localSet(key, value) { try { localStorage.setItem(key, value); } catch { } },

  prefersDark() { return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches; },

  // Saves bytes produced by an authenticated fetch (CSV export) as a file.
  downloadFile(fileName, contentType, base64) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
};

document.addEventListener("click", e => {
  if (e.target.closest && e.target.closest("#blazor-error-ui .dismiss")) {
    document.getElementById("blazor-error-ui").style.display = "none";
  }
});
