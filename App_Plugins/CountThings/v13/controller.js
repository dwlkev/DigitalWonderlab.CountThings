(function () {
    "use strict";
    angular.module("umbraco").controller("CountThingsV13Controller", function ($element, $timeout) {
        // run after Angular has linked the view DOM
        $timeout(function () {
            if (window.CountThingsV13 && typeof window.CountThingsV13.init === "function") {
                window.CountThingsV13.init($element[0]); // pass root element of the view
            }
        }, 0);
    });
})();
