(function () {
  "use strict";
  var toggle = document.querySelector(".nav-toggle");
  var nav = document.getElementById("site-nav");
  if (!toggle || !nav) return;

  var navItems = Array.prototype.slice.call(nav.querySelectorAll(".nav-item.has-submenu"));
  var CLOSE_DELAY = 300; // ms grace period so crossing the gap to the panel doesn't close it

  function isDesktop() {
    return window.innerWidth > 760;
  }

  function positionSubmenu(item) {
    var submenu = item.querySelector(".submenu");
    if (!submenu || !isDesktop()) return;
    submenu.classList.remove("align-right");
    var rect = submenu.getBoundingClientRect();
    if (rect.right > window.innerWidth - 8) {
      submenu.classList.add("align-right");
    }
  }

  function closeItem(item) {
    item.classList.remove("is-open");
    var btn = item.querySelector(".submenu-toggle");
    if (btn) btn.setAttribute("aria-expanded", "false");
    clearTimeout(item.__closeTimer);
  }

  function closeSubmenus() {
    navItems.forEach(closeItem);
  }

  function openItem(item) {
    clearTimeout(item.__closeTimer);
    navItems.forEach(function (other) {
      if (other !== item) closeItem(other);
    });
    item.classList.add("is-open");
    var btn = item.querySelector(".submenu-toggle");
    if (btn) btn.setAttribute("aria-expanded", "true");
    positionSubmenu(item);
  }

  function scheduleClose(item) {
    clearTimeout(item.__closeTimer);
    item.__closeTimer = setTimeout(function () {
      closeItem(item);
    }, CLOSE_DELAY);
  }

  function setOpen(open) {
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    toggle.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
    nav.classList.toggle("is-open", open);
    document.body.classList.toggle("nav-open", open);
    if (!open) closeSubmenus();
  }

  toggle.addEventListener("click", function () {
    setOpen(toggle.getAttribute("aria-expanded") !== "true");
  });

  navItems.forEach(function (item) {
    var btn = item.querySelector(".submenu-toggle");

    // Desktop: hovering the whole item (link, caret, or the panel itself)
    // opens it; leaving starts a grace-period timer instead of closing
    // immediately, so crossing the small gap to the panel is forgiven.
    item.addEventListener("mouseenter", function () {
      if (isDesktop()) openItem(item);
    });
    item.addEventListener("mouseleave", function () {
      if (isDesktop()) scheduleClose(item);
    });

    // Click/tap always works too — required on mobile (accordion), and
    // acts as an instant, no-delay open/close on desktop as well.
    if (btn) {
      btn.addEventListener("click", function (event) {
        event.preventDefault();
        event.stopPropagation();
        if (item.classList.contains("is-open")) {
          closeItem(item);
        } else {
          openItem(item);
        }
      });
    }
  });

  nav.addEventListener("click", function (event) {
    if (event.target.closest(".submenu-toggle")) return;
    if (event.target.closest("a")) setOpen(false);
  });

  document.addEventListener("click", function (event) {
    if (!isDesktop()) return; // mobile: nav-open handles its own dismissal
    if (!event.target.closest(".nav-item.has-submenu")) closeSubmenus();
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
      setOpen(false);
      closeSubmenus();
    }
  });

  window.addEventListener("resize", function () {
    if (window.innerWidth > 760) setOpen(false);
    else closeSubmenus();
  });
})();
