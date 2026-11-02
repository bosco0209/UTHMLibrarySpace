// UTHM Library Space — client helpers (chat widget, booking timeline, toasts, reveal animations)
(function () {
    // ---- Toasts auto-hide ----
    document.querySelectorAll('.app-toast').forEach(el =>
        setTimeout(() => el.classList.remove('show'), 6000));

    // ---- Reveal on scroll ----
    const io = 'IntersectionObserver' in window ? new IntersectionObserver(entries => {
        entries.forEach(e => {
            if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
        });
    }, { threshold: 0.08 }) : null;
    document.querySelectorAll('.reveal').forEach(el => io ? io.observe(el) : el.classList.add('in'));

    // ---- Live countdown for [data-countdown-end] elements ----
    //    (payment deadline on My Bookings and Proof pages)
    document.querySelectorAll('[data-countdown-end]').forEach(el => {
        const end = new Date(el.getAttribute('data-countdown-end')).getTime();
        if (isNaN(end)) { el.textContent = '—'; return; }

        function tick() {
            const ms = end - Date.now();
            if (ms <= 0) { el.textContent = 'expired'; return; }
            const h = Math.floor(ms / 3600000);
            const m = Math.floor((ms % 3600000) / 60000);
            const s = Math.floor((ms % 60000) / 1000);
            el.textContent = `${h}h ${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`;
        }

        tick();
        setInterval(tick, 1000);
    });

    // ---- Booking form: duration + timeline selection ----
    const start = document.getElementById('startTime');
    const end = document.getElementById('endTime');
    const track = document.getElementById('timelineTrack');
    const sel = document.getElementById('tlSelect');
    const toMin = v => { const [h, m] = (v || '0:0').split(':').map(Number); return h * 60 + m; };
    const toHm = m => String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0');

    function refreshBooking() {
        if (!start || !end) return;
        const s = toMin(start.value), e = toMin(end.value);
        const d = document.getElementById('durationText');
        if (d) d.textContent = e > s
            ? `${Math.floor((e - s) / 60)}h ${String((e - s) % 60).padStart(2, '0')}m`
            : '—';
        if (track && sel) {
            const o = toMin(track.dataset.open), c = toMin(track.dataset.close);
            if (e > s) {
                sel.style.display = 'block';
                sel.style.left = ((Math.max(s, o) - o) / (c - o) * 100) + '%';
                sel.style.width = ((Math.min(e, c) - Math.max(s, o)) / (c - o) * 100) + '%';
            } else sel.style.display = 'none';
        }
    }

    if (window.bookingDraft && start) {
        start.value = window.bookingDraft.start;
        end.value = window.bookingDraft.end;
        const p = document.getElementById('purpose');
        const a = document.getElementById('attendees');
        if (p) p.value = window.bookingDraft.Purpose || '';
        if (a) a.value = window.bookingDraft.Attendees || 1;
    }

    start && start.addEventListener('input', () => {
        if (toMin(end.value) <= toMin(start.value))
            end.value = toHm(Math.min(toMin(start.value) + 60, 22 * 60));
        refreshBooking();
    });
    end && end.addEventListener('input', refreshBooking);

    if (track && start) {
        track.addEventListener('click', ev => {
            const r = track.getBoundingClientRect();
            const o = toMin(track.dataset.open), c = toMin(track.dataset.close);
            let m = o + Math.round(((ev.clientX - r.left) / r.width) * (c - o) / 30) * 30;
            m = Math.min(Math.max(m, o), c - 60);
            start.value = toHm(m);
            end.value = toHm(m + 60);
            refreshBooking();
        });
    }
    refreshBooking();

    // ---- Booking date change: reload with ?date=YYYY-MM-DD ----
    const bookDate = document.querySelector('[data-testid=booking-date-input]');
    const rentalPeriod = document.getElementById('rentalPeriod');
    function refreshRental() {
        if (!rentalPeriod || !bookDate.value) return;
        const s = new Date(bookDate.value + 'T00:00:00');
        // Same rule as the server: start + 1 calendar month - 1 day (clamped to month length).
        const last = new Date(s.getFullYear(), s.getMonth() + 2, 0).getDate();
        const e = new Date(s.getFullYear(), s.getMonth() + 1, Math.min(s.getDate(), last));
        e.setDate(e.getDate() - 1);
        const fmt = d => d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
        rentalPeriod.textContent = `${fmt(s)} → ${fmt(e)}`;
    }
    refreshRental();
    bookDate && bookDate.addEventListener('change', () => {
        if (!bookDate.value) return;
        if (rentalPeriod) { refreshRental(); return; }
        const u = new URL(location.href);
        u.searchParams.set('date', bookDate.value);
        location.href = u.toString();
    });

    // ---- Chat widget ----
    const widget = document.getElementById('chatWidget');
    if (!widget) return;

    const panel = document.getElementById('chatPanel');
    const body = document.getElementById('chatBody');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatText');
    const toggle = document.getElementById('chatToggle');
    const closeB = document.getElementById('chatClose');
    const sugg = document.querySelectorAll('#chatSuggest button');
    const history = [];

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
    const md = s => esc(s)
        .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
        .replace(/\n/g, '<br>');

    function open(v) {
        widget.classList.toggle('open', v);
        panel && panel.setAttribute('aria-hidden', String(!v));
        if (v && input) setTimeout(() => input.focus(), 150);
    }
    window.openChat = () => open(true);

    toggle && toggle.addEventListener('click', () => open(!widget.classList.contains('open')));
    closeB && closeB.addEventListener('click', () => open(false));

    function add(role, html, extra) {
        const div = document.createElement('div');
        div.className = 'msg ' + role + (extra ? ' ' + extra : '');
        div.innerHTML = html;
        div.setAttribute('data-testid', role === 'user' ? 'chatbot-user-message' : 'chatbot-bot-message');
        body.appendChild(div);
        body.scrollTop = body.scrollHeight;
        return div;
    }

    async function send(text) {
        text = (text || '').trim();
        if (!text) return;
        add('user', esc(text));
        if (input) input.value = '';
        const typing = add('bot', '<span class="typing"><i></i><i></i><i></i></span>', 'pending');
        try {
            const res = await fetch('/Chat/Ask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: text, history })
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const data = await res.json();
            typing.remove();
            const reply = data.reply || data.error || 'Sorry, something went wrong.';
            add('bot', md(reply) +
                (data.source === 'faq'
                    ? '<div class="msg-src">Library FAQ</div>'
                    : '<div class="msg-src">Gemini</div>'));
            history.push({ role: 'user', text }, { role: 'model', text: reply });
            if (history.length > 20) history.splice(0, history.length - 20);
        } catch {
            typing.remove();
            add('bot', 'I could not reach the server. Please try again in a moment.');
        }
    }

    form && form.addEventListener('submit', e => { e.preventDefault(); send(input.value); });
    sugg.forEach(b => b.addEventListener('click', () => send(b.dataset.q)));
})();