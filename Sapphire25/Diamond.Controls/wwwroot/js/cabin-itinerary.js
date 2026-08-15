window.diamondCabinItinerary = {
	scrollHere: function (host) {
		if (!host) {
			return;
		}
		var el = host.querySelector(".cabin-itinerary-here");
		if (!el) {
			return;
		}
		el.scrollIntoView({ block: "center", inline: "nearest", behavior: "smooth" });
	}
};
