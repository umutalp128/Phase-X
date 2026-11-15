// Web build only. In a browser, Application.persistentDataPath lives in an in-memory file system that is copied to
// IndexedDB only when FS.syncfs runs. Without it a save file is lost when the tab closes.
mergeInto(LibraryManager.library, {

  // Flush the in-memory file system to IndexedDB (false = memory -> IndexedDB).
  PhaseXSyncFiles: function () {
    try {
      FS.syncfs(false, function (err) {
        if (err) console.warn('[PhaseX Save] IndexedDB sync failed: ' + err);
      });
    } catch (e) {
      console.warn('[PhaseX Save] IndexedDB sync is not available: ' + e);
    }
  },

  // A browser never tells the game it is quitting. Ask the named GameObject to save when the page is hidden,
  // closed or navigated away from; that is the last moment a write can still start.
  PhaseXRegisterPageHide: function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);
    if (typeof window === 'undefined' || window.__phaseXPageHideHooked) return;
    window.__phaseXPageHideHooked = true;
    var notify = function () {
      try { SendMessage(objectName, methodName); } catch (e) { /* the game is already gone */ }
    };
    window.addEventListener('pagehide', notify);
    window.addEventListener('beforeunload', notify);
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') notify();
    });
  }
});
