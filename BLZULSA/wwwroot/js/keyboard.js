
window.osk = (function () {
    'use strict';

    var enabled = false;
    var target = null;      
    var root = null;        
    var shift = false;
    var symbols = false;

    var LETTERS = [
        ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
        ['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p', 'å'],
        ['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'ö', 'ä'],
        ['z', 'x', 'c', 'v', 'b', 'n', 'm', ',', '.', '-']
    ];

   
    var SYMBOLS = [
        ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
        ['@', '.', ':', '/', '-', '_', '?', '=', '&', '#'],
        ['+', '*', '%', '(', ')', '[', ']', '{', '}', '~'],
        ['"', "'", '!', ';', ',', '<', '>', '|', '\\', '^']
    ];

    function typable(el) {
        if (!el || el.disabled || el.readOnly) { return false; }
        if (el.tagName === 'TEXTAREA') { return true; }
        if (el.tagName !== 'INPUT') { return false; }
        return ['text', 'password', 'number', 'search', 'email', 'url', 'tel'].indexOf(el.type) >= 0;
    }

   
    function commit(el) {
        el.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function insert(text) {
        if (!target) { return; }

        if (target.type === 'number') {
            target.value += text;
            commit(target);
            return;
        }

        var start = target.selectionStart !== null ? target.selectionStart : target.value.length;
        var end = target.selectionEnd !== null ? target.selectionEnd : start;

        target.value = target.value.slice(0, start) + text + target.value.slice(end);
        target.setSelectionRange(start + text.length, start + text.length);
        commit(target);
    }

    function backspace() {
        if (!target) { return; }

        if (target.type === 'number') {
            target.value = target.value.slice(0, -1);
            commit(target);
            return;
        }

        var start = target.selectionStart !== null ? target.selectionStart : target.value.length;
        var end = target.selectionEnd !== null ? target.selectionEnd : start;

        if (start === end) {
            if (start === 0) { return; }
            target.value = target.value.slice(0, start - 1) + target.value.slice(end);
            target.setSelectionRange(start - 1, start - 1);
        } else {
            target.value = target.value.slice(0, start) + target.value.slice(end);
            target.setSelectionRange(start, start);
        }

        commit(target);
    }

    function clear() {
        if (!target) { return; }
        target.value = '';
        commit(target);
    }

    function key(label, cls, action, wide) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'osk__key' + (cls ? ' ' + cls : '');
        if (wide) { b.style.flex = wide; }
        b.textContent = label;

        b.addEventListener('pointerdown', function (e) { e.preventDefault(); });
        b.addEventListener('click', function (e) { e.preventDefault(); action(); });
        return b;
    }

    function render() {
        if (!root) { return; }
        root.innerHTML = '';

        var rows = symbols ? SYMBOLS : LETTERS;

        rows.forEach(function (chars, i) {
            var row = document.createElement('div');
            row.className = 'osk__row';

            if (!symbols && i === 3) {
                row.appendChild(key(shift ? '⇧' : '⇧', 'osk__key--mod' + (shift ? ' is-on' : ''), function () {
                    shift = !shift;
                    render();
                }, '1.6'));
            }

            chars.forEach(function (c) {
                var label = (!symbols && shift) ? c.toUpperCase() : c;
                row.appendChild(key(label, null, function () {
                    insert(label);
                    if (shift) { shift = false; render(); }  
                }));
            });

            if (i === 3) {
                row.appendChild(key('⌫', 'osk__key--mod', backspace, '1.6'));
            }

            root.appendChild(row);
        });

        var last = document.createElement('div');
        last.className = 'osk__row';
        last.appendChild(key(symbols ? 'ABC' : '?123', 'osk__key--mod', function () {
            symbols = !symbols;
            shift = false;
            render();
        }, '1.6'));
        last.appendChild(key('Rensa', 'osk__key--mod', clear, '1.4'));
        last.appendChild(key(' ', 'osk__key--space', function () { insert(' '); }, '5'));
        last.appendChild(key('Klar', 'osk__key--done', function () {
            if (target) { target.blur(); }
            hide();
        }, '1.8'));
        root.appendChild(last);
    }

    function show() {
        if (!root) {
            root = document.createElement('div');
            root.className = 'osk';
            document.body.appendChild(root);
        }

        render();
        root.classList.add('is-open');
        document.body.classList.add('osk-open');
      
        if (target && target.scrollIntoView) {
            setTimeout(function () { target.scrollIntoView({ block: 'center', behavior: 'smooth' }); }, 50);
        }
    }

    function hide() {
        if (root) { root.classList.remove('is-open'); }
        document.body.classList.remove('osk-open');
        target = null;
    }

    document.addEventListener('focusin', function (e) {
        if (!enabled) { return; }

        if (typable(e.target)) {
            target = e.target;
            show();
        }
    });

    document.addEventListener('focusout', function () {
        if (!enabled) { return; }

        setTimeout(function () {
            if (!typable(document.activeElement)) { hide(); }
        }, 120);
    });

    return {
        setEnabled: function (value) {
            enabled = !!value;
            if (!enabled) { hide(); }
        },
        isEnabled: function () { return enabled; },
        isOpen: function () { return !!root && root.classList.contains('is-open'); }
    };
})();
