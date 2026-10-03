(function () {
  function closeNotifs() {
    document.querySelectorAll(".cc-notif-popover, .notifications-popover").forEach(function (p) {
      p.classList.remove("show");
    });
  }
  function closeProfiles() {
    document.querySelectorAll("[data-cc-profile-dd], .cc-top-dd, #profileDropdown, .dropdown").forEach(function (p) {
      p.classList.remove("show");
    });
  }
  function closeAllMenus() {
    closeNotifs();
    closeProfiles();
  }

  async function loadNotifList(listEl, dotEl) {
    if (!listEl) return;
    listEl.innerHTML = '<div class="cc-notif-empty">Loading…</div>';
    try {
      var res = await fetch("/api/notifications");
      if (!res.ok) throw new Error("fail");
      var data = await res.json();
      var items = data.notifications || [];
      if (dotEl) {
        if ((data.unreadCount || 0) > 0) dotEl.classList.remove("hidden");
        else dotEl.classList.add("hidden");
      }
      if (!items.length) {
        listEl.innerHTML = '<div class="cc-notif-empty">No notifications yet</div>';
        return;
      }
      listEl.innerHTML = items.slice(0, 8).map(function (n) {
        var isAlert = (n.type || "").toLowerCase() === "alert";
        var icon = isAlert ? "fa-triangle-exclamation" : "fa-circle-info";
        var color = isAlert ? "#f43f5e" : "#0ea5e9";
        var cls = n.isRead ? "cc-notif-item" : "cc-notif-item unread";
        return (
          '<div class="' + cls + '">' +
          '<i class="fa-solid ' + icon + '" style="color:' + color + ';margin-top:2px;font-size:12px;"></i>' +
          "<div><p>" + (n.message || "") + "</p><span>" + (n.createdAt || "") + "</span></div></div>"
        );
      }).join("");
    } catch (e) {
      listEl.innerHTML = '<div class="cc-notif-empty">Could not load notifications</div>';
    }
  }

  function bindNotifButtons() {
    document.querySelectorAll(".cc-notif-btn").forEach(function (btn) {
      if (btn.dataset.ccBound === "1") return;
      btn.dataset.ccBound = "1";
      btn.addEventListener("click", function (e) {
        e.preventDefault();
        e.stopPropagation();
        var wrap = btn.closest(".cc-notif-wrap") || btn.closest(".dropdown-wrap");
        var pop = wrap ? wrap.querySelector(".cc-notif-popover") : null;
        if (!pop) return;
        var willOpen = !pop.classList.contains("show");
        closeAllMenus();
        if (willOpen) {
          pop.classList.add("show");
          loadNotifList(pop.querySelector(".cc-notif-list"), btn.querySelector(".cc-notif-dot"));
        }
      });
    });

    document.querySelectorAll(".cc-notif-markall").forEach(function (btn) {
      if (btn.dataset.ccBound === "1") return;
      btn.dataset.ccBound = "1";
      btn.addEventListener("click", async function (e) {
        e.preventDefault();
        e.stopPropagation();
        try {
          await fetch("/api/notifications/read-all", { method: "PUT" });
        } catch (_) {}
        var wrap = btn.closest(".cc-notif-wrap") || btn.closest(".dropdown-wrap");
        var pop = wrap ? wrap.querySelector(".cc-notif-popover") : null;
        var bell = wrap ? wrap.querySelector(".cc-notif-btn") : null;
        if (pop) {
          loadNotifList(
            pop.querySelector(".cc-notif-list"),
            bell ? bell.querySelector(".cc-notif-dot") : null
          );
        }
      });
    });
  }

  function bindProfileButtons() {
    document.querySelectorAll("[data-cc-profile-btn], #profileMenuBtn").forEach(function (btn) {
      if (btn.dataset.ccBound === "1") return;
      btn.dataset.ccBound = "1";
      btn.addEventListener("click", function (e) {
        e.preventDefault();
        e.stopPropagation();
        var wrap = btn.closest(".dropdown-wrap") || btn.parentElement;
        var dd = wrap
          ? wrap.querySelector("[data-cc-profile-dd]") ||
            wrap.querySelector(".cc-top-dd") ||
            wrap.querySelector("#profileDropdown")
          : null;
        if (!dd) return;
        var willOpen = !dd.classList.contains("show");
        closeAllMenus();
        if (willOpen) dd.classList.add("show");
      });
    });
  }

  function bindOutsideClick() {
    if (window.__ccHeaderOutsideBound) return;
    window.__ccHeaderOutsideBound = true;
    document.addEventListener("click", function (e) {
      if (!e.target.closest(".dropdown-wrap") && !e.target.closest(".cc-notif-wrap")) {
        closeAllMenus();
      }
    });
  }

  async function prefetchDots() {
    document.querySelectorAll(".cc-notif-btn .cc-notif-dot").forEach(async function (dot) {
      try {
        var res = await fetch("/api/notifications");
        if (!res.ok) return;
        var data = await res.json();
        if ((data.unreadCount || 0) > 0) dot.classList.remove("hidden");
      } catch (_) {}
    });
  }

  function init() {
    bindNotifButtons();
    bindProfileButtons();
    bindOutsideClick();
    prefetchDots();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
