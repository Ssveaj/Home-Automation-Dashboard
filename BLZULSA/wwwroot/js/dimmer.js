window.dimmer = (function () {
    'use strict';

    var LAGSTA = 5;

    var TROSKEL = 8;

    var dotnet = null;
    var drag = null;

    function nivaPa(kort) {
        var v = parseFloat(kort.style.getPropertyValue('--niva'));
        return isNaN(v) ? 0 : v;
    }

    function rita(kort, niva) {
        kort.style.setProperty('--niva', niva + '%');

        var text = kort.querySelector('.device__state');
        if (text) {
            text.textContent = niva < LAGSTA ? 'Av' : 'På · ' + niva + ' %';
        }
    }

    function ner(e) {
        if (e.button !== 0) { return; }

        var kort = e.target.closest('.device--dimmer');
        if (!kort) { return; }

        var knapp = kort.querySelector('.device__toggle');
        if (!knapp || knapp.disabled) { return; }

        var text = kort.querySelector('.device__state');

        drag = {
            kort: kort,
            id: e.pointerId,
            x: e.clientX,
            y: e.clientY,
            bredd: kort.getBoundingClientRect().width,
            start: nivaPa(kort),
            startText: text ? text.textContent : null,
            niva: nivaPa(kort),
            aktivt: false
        };
    }

    function flytt(e) {
        if (!drag || e.pointerId !== drag.id) { return; }

        var dx = e.clientX - drag.x;
        var dy = e.clientY - drag.y;

        if (!drag.aktivt) {
            if (Math.abs(dy) > TROSKEL && Math.abs(dy) > Math.abs(dx)) {
                drag = null;
                return;
            }

            if (Math.abs(dx) < TROSKEL) { return; }

            drag.aktivt = true;
            drag.kort.classList.add('is-dragging');

            try { drag.kort.setPointerCapture(drag.id); } catch (_) { }
        }

        var niva = Math.round(drag.start + dx / drag.bredd * 100);
        drag.niva = Math.max(0, Math.min(100, niva));

        rita(drag.kort, drag.niva);
        e.preventDefault();
    }

    function upp(e) {
        if (!drag || e.pointerId !== drag.id) { return; }

        var d = drag;
        drag = null;

        if (!d.aktivt) { return; }

        d.kort.classList.remove('is-dragging');

        if (dotnet) {
            dotnet.invokeMethodAsync('Dragit', d.kort.dataset.key, d.niva)
                .catch(function (fel) { console.warn('dimmer: kunde inte skicka nivån', fel); });
        }
    }

    function avbryt(e) {
        if (!drag || e.pointerId !== drag.id) { return; }

        var d = drag;
        drag = null;

        if (!d.aktivt) { return; }

        d.kort.classList.remove('is-dragging');
        d.kort.style.setProperty('--niva', d.start + '%');

        var text = d.kort.querySelector('.device__state');
        if (text && d.startText !== null) { text.textContent = d.startText; }
    }

    document.addEventListener('pointerdown', ner);
    document.addEventListener('pointermove', flytt, { passive: false });
    document.addEventListener('pointerup', upp);
    document.addEventListener('pointercancel', avbryt);

    return {
        init: function (referens) { dotnet = referens; }
    };
})();
