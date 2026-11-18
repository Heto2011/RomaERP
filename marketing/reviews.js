/* Customer reviews on a product's own page. Put <section id="reviews" hidden data-products="HR"> ... </section> on the page and load this
   script. It shows only reviews of that product that are approved and that the customer agreed to publish; with none, the section stays hidden.
   ERP page: data-products="ERP,POS,Books" (the ERP page covers all three); HR page: data-products="HR". */
(function () {
  var section = document.getElementById("reviews");
  if (!section) return;
  var products = section.getAttribute("data-products") || "";
  var ar = document.documentElement.lang === "ar";
  var T = ar
    ? { eyebrow: "آراء العملاء", heading: "اللي قالوه عننا", summary: "متوسط {avg} من 5، من {n} رأي حقيقي لعملاء وافقوا على النشر" }
    : { eyebrow: "Customer reviews", heading: "What customers say", summary: "Average {avg} out of 5, from {n} real reviews by customers who agreed to publish them" };
  fetch("/api/marketing/reviews?product=" + encodeURIComponent(products))
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (d) {
      if (!d || !d.count) return;
      section.hidden = false;
      section.querySelector(".reviews-eyebrow").textContent = T.eyebrow;
      section.querySelector(".reviews-heading").textContent = T.heading;
      // The average and count appear from five reviews up, so a single opinion is not passed off as a trend.
      section.querySelector(".reviews-summary").textContent = d.count >= 5 ? T.summary.replace("{avg}", d.average).replace("{n}", d.count) : "";
      var grid = section.querySelector(".reviews-grid");
      grid.textContent = "";
      d.items.forEach(function (r) {
        var card = document.createElement("div"); card.className = "review-card";
        var stars = document.createElement("div"); stars.className = "stars"; stars.textContent = "★".repeat(r.rating) + "☆".repeat(5 - r.rating);
        var text = document.createElement("p"); text.textContent = r.comment || "";
        var who = document.createElement("div"); who.className = "who"; who.textContent = r.companyName ? r.displayName + " — " + r.companyName : r.displayName;
        card.appendChild(stars); card.appendChild(text); card.appendChild(who); grid.appendChild(card);
      });
    })
    .catch(function () {});
})();
