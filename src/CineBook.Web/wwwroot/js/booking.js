$(function () {
    var $page = $('#booking-page');
    if (!$page.length) return;

    var showtimeId = $page.attr('data-showtime');
    var token = $('input[name=__RequestVerificationToken]').first().val();
    var STEPS = ['seats', 'extras', 'confirm'];
    var SEAT_TYPE_LABEL = { Standard: 'Thường', Vip: 'VIP', Couple: 'Ghế đôi', Accessible: 'Ưu tiên' };
    var AISLE_FROM = 8;

    function cx() {
        var out = [];
        for (var i = 0; i < arguments.length; i++) if (arguments[i]) out.push(arguments[i]);
        return out.join(' ');
    }

    $('[data-abort]').on('click', function () {
        if (state && state.step !== 'confirm') {
            var list = chosen();
            var body = list.length > 0
                ? '<p>Các ghế bạn đang chọn (<b class="font-mono text-cream">' +
                  CB.escape(list.map(function (s) { return s.label; }).join(', ')) +
                  '</b>) sẽ bị bỏ và bạn quay về trang chủ.</p>'
                : '<p>Bạn sẽ thoát khỏi trang đặt vé và quay về trang chủ.</p>';
            $('#abort-dialog [data-confirm-body]').html(body);
        }
        $('#abort-dialog [data-confirm-error]').hide();
        CB.openModal('#abort-dialog');
    });

    if ($('#abort-dialog [data-confirm-error]').text().trim()) {
        CB.openModal('#abort-dialog');
    }

    if ($page.attr('data-step') === 'confirm') {
        initConfirm();
        var state = null;
        return;
    }

    var state = {
        step: $page.attr('data-step') || 'seats',
        selected: ($page.attr('data-selected') || '').split(',').filter(Boolean),
        promo: $page.attr('data-promo') || '',
        error: $page.attr('data-error') || null,
        seatMap: null,
        seatById: {},
        fetching: false,
        timer: null
    };

    function maxSeats() {
        return state.seatMap ? state.seatMap.maxSeatsPerBooking : 8;
    }

    function chosen() {
        return state.selected
            .map(function (id) { return state.seatById[id]; })
            .filter(Boolean);
    }

    function comboRows() {
        return $('[data-combo]').map(function () {
            var $row = $(this);
            return {
                id: $row.attr('data-combo'),
                name: $row.attr('data-name'),
                price: Number($row.attr('data-price')),
                qty: Number($row.find('[data-qty-value]').attr('data-qty-value'))
            };
        }).get();
    }

    function seatsTotal() {
        return chosen().reduce(function (sum, s) { return sum + s.price; }, 0);
    }

    function combosTotal() {
        return comboRows().reduce(function (sum, c) { return sum + c.price * c.qty; }, 0);
    }

    function loadSeatMap() {
        state.fetching = true;
        $('[data-refresh] svg').addClass('animate-spin');
        $.getJSON('/Booking/SeatMap/' + showtimeId, function (data) {
            state.seatMap = data;
            state.seatById = {};
            data.seats.forEach(function (s) { state.seatById[s.seatId] = s; });
            // Server là nguồn sự thật: ghế đang chọn = ghế mình đang khoá tạm (trừ ghế đang chờ server trả lời).
            state.selected = state.selected.filter(function (id) {
                return pendingSeats[id] || (state.seatById[id] && state.seatById[id].heldByMe);
            });
            data.seats.forEach(function (s) {
                if (s.heldByMe && !pendingSeats[s.seatId] && state.selected.indexOf(s.seatId) < 0) state.selected.push(s.seatId);
            });
            renderSeats();
            renderAside();
        }).always(function () {
            state.fetching = false;
            $('[data-refresh] svg').removeClass('animate-spin');
            if (state.refetch) {
                state.refetch = false;
                loadSeatMap();
            }
        });
    }

    var pendingSeats = {};

    function seatRequest(action, id, done) {
        pendingSeats[id] = true;
        $.post('/Booking/' + action + '/' + showtimeId, { seatId: id, __RequestVerificationToken: token })
            .done(function (r) { if (done) done(r); })
            .fail(function () { if (done) done({ ok: false, message: 'Không kết nối được máy chủ, vui lòng thử lại.' }); })
            .always(function () {
                delete pendingSeats[id];
                renderSeats();
                renderAside();
            });
    }

    function seatTitle(seat, taken) {
        if (taken) {
            if (seat.status === 'Sold') return seat.label + ' — đã bán';
            if (seat.status === 'Maintenance') return seat.label + ' — ghế bảo trì';
            return seat.label + ' — người khác đang giữ';
        }
        return seat.label + ' · ' + SEAT_TYPE_LABEL[seat.seatType] + ' · ' + CB.money(seat.price);
    }

    function seatButton(seat, selected, blocked) {
        var taken = seat.status === 'Sold' || seat.status === 'Maintenance' ||
            (seat.status === 'Held' && !seat.heldByMe);
        var disabled = taken || (blocked && !selected);
        var couple = seat.seatType === 'Couple';
        var title = CB.escape(seatTitle(seat, taken));
        var cls = cx(
            'relative h-7 rounded-t-lg rounded-b-[5px] border text-[10px] font-medium',
            'transition-all duration-150',
            couple ? 'w-[3.75rem]' : 'w-7',
            selected && 'scale-105 border-brand-400 bg-brand-500 text-on-brand shadow-[0_0_0_3px_rgba(229, 9, 20,.2)]',
            !selected && (seat.status === 'Available' || (seat.status === 'Held' && seat.heldByMe)) &&
                'border-ink-600 bg-ink-800 text-muted hover:-translate-y-0.5 hover:border-brand-500 hover:bg-ink-700 hover:text-cream',
            !selected && seat.status === 'Held' && !seat.heldByMe &&
                'cursor-not-allowed border-amber-500/50 bg-amber-950 text-amber-400',
            seat.status === 'Sold' && 'cursor-not-allowed border-ink-700 bg-ink-700 text-ink-500',
            seat.status === 'Maintenance' && 'cursor-not-allowed border-dashed border-ink-600 bg-transparent text-ink-500',
            disabled && !taken && 'cursor-not-allowed opacity-35'
        );
        var vip = seat.seatType === 'Vip' && !selected && seat.status === 'Available'
            ? '<span aria-hidden="true" class="absolute inset-x-0 -bottom-[3px] mx-auto h-[3px] w-3 rounded-full bg-brand-600/70"></span>'
            : '';
        return '<button type="button" data-seat="' + seat.seatId + '" title="' + title + '" aria-label="' + title +
            '" aria-pressed="' + selected + '"' + (disabled ? ' disabled=""' : '') + ' class="' + cls + '">' +
            seat.number + vip + '</button>';
    }

    function renderSeats() {
        if (!state.seatMap) return;
        var groups = {};
        state.seatMap.seats.forEach(function (s) {
            (groups[s.rowLabel] = groups[s.rowLabel] || []).push(s);
        });
        var labels = Object.keys(groups).sort(function (a, b) { return a.localeCompare(b); });
        var atLimit = state.selected.length >= maxSeats();
        var rowLabel = function (label) {
            return '<span class="w-4 shrink-0 text-center font-mono text-[11px] text-faint">' + CB.escape(label) + '</span>';
        };

        var html = labels.map(function (label) {
            var list = groups[label].sort(function (a, b) { return a.number - b.number; });
            var aisleAt = list.length >= AISLE_FROM ? Math.ceil(list.length / 2) : -1;
            var seats = list.map(function (seat, i) {
                var selected = state.selected.indexOf(seat.seatId) >= 0;
                return '<div class="flex items-center">' +
                    (i === aisleAt ? '<span class="w-6 sm:w-8" aria-hidden="true"></span>' : '') +
                    seatButton(seat, selected, atLimit && !selected) + '</div>';
            }).join('');
            return '<div class="flex items-center gap-3">' + rowLabel(label) +
                '<div class="flex items-center gap-1.5">' + seats + '</div>' + rowLabel(label) + '</div>';
        }).join('');
        $('#seat-rows').html(html);
    }

    function sumRow(label, value, tone) {
        return '<div class="flex items-center justify-between py-0.5"><span class="text-faint">' + label +
            '</span><span class="' + cx('tabular', tone === 'mint' && 'text-mint-400') + '">' + CB.money(value) + '</span></div>';
    }

    function renderAside() {
        var list = chosen();
        var seatHtml = '<p class="mb-2 text-[13px] font-semibold text-muted">Ghế đã chọn</p>';
        if (list.length === 0) {
            seatHtml += '<p class="text-[13px] text-faint">Chưa chọn ghế nào</p>';
        } else {
            seatHtml += '<ul class="flex flex-col gap-1.5">' + list.map(function (s) {
                return '<li class="flex items-center justify-between text-[13px]"><span><b class="font-mono">' +
                    CB.escape(s.label) + '</b><span class="ml-1.5 text-faint">' + SEAT_TYPE_LABEL[s.seatType] +
                    '</span></span><span class="tabular">' + CB.money(s.price) + '</span></li>';
            }).join('') + '</ul>';
        }
        $('[data-chosen]').html(seatHtml);

        var combos = comboRows();
        var cTotal = combosTotal();
        var sTotal = seatsTotal();
        if (cTotal > 0) {
            $('[data-combo-summary]').html('<p class="mb-2 text-[13px] font-semibold text-muted">Bắp nước</p>' +
                '<ul class="flex flex-col gap-1.5">' + combos.filter(function (c) { return c.qty > 0; }).map(function (c) {
                    return '<li class="flex items-center justify-between gap-3 text-[13px]"><span class="min-w-0 truncate">' +
                        CB.escape(c.name) + ' × ' + c.qty + '</span><span class="shrink-0 tabular">' +
                        CB.money(c.price * c.qty) + '</span></li>';
                }).join('') + '</ul>').show();
        } else {
            $('[data-combo-summary]').empty().hide();
        }

        $('[data-summary]').html(sumRow('Tiền ghế', sTotal) + (cTotal > 0 ? sumRow('Bắp nước', cTotal) : '') +
            '<div class="mt-2.5 flex items-center justify-between border-t border-ink-700 pt-2.5"><span class="font-semibold">Tạm tính</span>' +
            '<span class="font-display text-xl font-extrabold text-brand-400 tabular">' + CB.money(sTotal + cTotal) + '</span></div>');

        if (state.error) $('[data-aside-error]').text(state.error).show();
        else $('[data-aside-error]').empty().hide();

        $('[data-next]').toggle(state.step === 'seats').prop('disabled', list.length === 0);
        $('[data-extras-actions]').toggle(state.step === 'extras');
    }

    function renderSteps() {
        var index = STEPS.indexOf(state.step);
        $('[data-step-index]').each(function () {
            var i = Number($(this).attr('data-step-index'));
            var s = i < index ? 'done' : i === index ? 'active' : 'todo';
            $(this).find('[data-step-bar]').attr('class', cx('block h-px bg-brand-500 transition-all duration-500', s === 'todo' ? 'w-0' : 'w-full'));
            $(this).find('[data-step-dot]').attr('class', cx(
                'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[11px] font-bold transition-all',
                s === 'active' && 'bg-brand-500 text-on-brand shadow-[0_0_0_4px_rgba(229, 9, 20,.15)]',
                s === 'done' && 'bg-mint-950 text-mint-400',
                s === 'todo' && 'border border-ink-600 text-faint'
            )).html(s === 'done' ? CB.icon('check', 13) : String(i + 1));
            $(this).find('[data-step-label]').attr('class', cx('hidden sm:inline', s === 'todo' ? 'text-faint' : 'text-cream'));
        });
    }

    function startPolling() {
        stopPolling();
        state.timer = setInterval(loadSeatMap, 5000);
    }

    function stopPolling() {
        if (state.timer) clearInterval(state.timer);
        state.timer = null;
    }

    function setStep(step) {
        state.step = step;
        $('[data-panel]').each(function () { $(this).toggle($(this).attr('data-panel') === step); });
        renderSteps();
        renderAside();
        if (step === 'seats') startPolling(); else stopPolling();
        if (step === 'extras') mountVouchers(); else unmountVouchers();
    }

    $(document).on('click', '[data-seat]', function () {
        var id = $(this).attr('data-seat');
        if (pendingSeats[id]) return;
        state.error = null;
        var at = state.selected.indexOf(id);
        if (at >= 0) {
            state.selected.splice(at, 1);
            seatRequest('ReleaseSeat', id);
        } else if (state.selected.length < maxSeats()) {
            state.selected.push(id);
            seatRequest('SelectSeat', id, function (r) {
                if (r.ok) return;
                if (r.code === 'SELECT_TIMEOUT') return selectTimedOut();
                var i = state.selected.indexOf(id);
                if (i >= 0) state.selected.splice(i, 1);
                state.error = r.message;
                loadSeatMap();
            });
        }
        renderSeats();
        renderAside();
    });

    $('[data-refresh]').on('click', loadSeatMap);
    $('[data-next]').on('click', function () { setStep('extras'); });
    $('[data-back-seats]').on('click', function () { setStep('seats'); });

    $('[data-qty]').on('click', function () {
        var $row = $(this).closest('[data-combo]');
        var $value = $row.find('[data-qty-value]');
        var qty = Number($value.attr('data-qty-value')) + Number($(this).attr('data-qty'));
        qty = Math.max(0, Math.min(9, qty));
        $value.attr('data-qty-value', qty).text(qty);
        $row.find('[data-qty="-1"]').prop('disabled', qty === 0);
        $row.find('[data-qty="1"]').prop('disabled', qty >= 9);
        renderAside();
        if (state.step === 'extras') loadVouchers();
    });

    $('#hold-form').on('submit', function () {
        var $form = $(this);
        $form.find('[data-generated]').remove();
        var add = function (name, value) {
            $form.append($('<input type="hidden" data-generated>').attr('name', name).val(value));
        };
        state.selected.forEach(function (id) { add('SeatIds', id); });
        comboRows().filter(function (c) { return c.qty > 0; }).forEach(function (c, i) {
            add('Combos[' + i + '].ComboId', c.id);
            add('Combos[' + i + '].Quantity', c.qty);
        });
        add('PromotionCode', state.promo.trim());
        CB.setLoading($('[data-hold]'), true);
        clearInterval(selectTimer);
    });

    var $selectCountdown = $('[data-select-countdown]');
    var selectExpires = new Date($selectCountdown.attr('data-select-countdown')).getTime();

    function tickSelect() {
        var left = Math.max(0, Math.floor((selectExpires - Date.now()) / 1000));
        var urgent = left <= 60;
        $selectCountdown.attr('class', cx('flex items-center gap-2 rounded-xl border px-3.5 py-2',
            urgent ? 'border-velvet-500/45 bg-velvet-950' : 'border-brand-600/40 bg-brand-950'));
        $selectCountdown.children('svg').attr('class', 'lucide lucide-timer ' + (urgent ? 'text-velvet-400' : 'text-brand-400'));
        $selectCountdown.find('[data-countdown-text]').attr('class', cx('font-mono text-lg font-bold tabular',
            urgent ? 'text-velvet-400' : 'text-brand-400')).text(CB.countdown(left));
        return left;
    }

    function selectTimedOut() {
        clearInterval(selectTimer);
        stopPolling();
        location.replace($selectCountdown.attr('data-timeout-url'));
    }

    tickSelect();
    var selectTimer = setInterval(function () {
        if (tickSelect() <= 0) selectTimedOut();
    }, 1000);

    if (window.signalR) {
        var hub = new signalR.HubConnectionBuilder().withUrl('/hubs/seats').withAutomaticReconnect().build();
        var joinShowtime = function () { return hub.invoke('JoinShowtime', showtimeId); };
        hub.on('SeatsChanged', function () {
            if (state.step !== 'seats') return;
            if (state.fetching) state.refetch = true;
            else loadSeatMap();
        });
        hub.onreconnected(joinShowtime);
        hub.start().then(joinShowtime).catch(function () { });
    }

    var voucher = { mounted: false, amount: null, cache: {}, showInput: false, manual: '', error: null };

    function mountVouchers() {
        voucher.mounted = true;
        loadVouchers();
    }

    function unmountVouchers() {
        voucher.mounted = false;
        voucher.showInput = false;
        voucher.manual = '';
        voucher.error = null;
        $('#voucher-picker').empty();
    }

    function loadVouchers() {
        if (!voucher.mounted) return;
        var amount = seatsTotal() + combosTotal();
        voucher.amount = amount;
        if (voucher.cache[amount]) renderVouchers();
        else $('#voucher-picker').html('<div class="flex flex-col gap-2.5"><div class="skeleton rounded-lg h-20 rounded-xl"></div><div class="skeleton rounded-lg h-20 rounded-xl"></div></div>');
        $.getJSON('/Booking/Vouchers', { amount: amount }, function (data) {
            voucher.cache[amount] = data;
            if (voucher.mounted && voucher.amount === amount) renderVouchers();
        });
    }

    function invalidateVouchers() {
        voucher.cache = {};
        var amount = voucher.amount;
        $.getJSON('/Booking/Vouchers', { amount: amount }, function (data) {
            voucher.cache[amount] = data;
            if (voucher.mounted && voucher.amount === amount) renderVouchers();
        });
    }

    function voucherRow(v) {
        var value = state.promo || null;
        var active = v.code === value;
        var savedLabel = v.saved ? 'Bỏ khỏi kho voucher' : 'Lưu vào kho voucher';
        return '<div class="' + cx('relative flex items-stretch overflow-hidden rounded-xl border transition-all',
                active ? 'border-brand-500 bg-brand-950/70 shadow-[0_0_0_3px_rgba(229, 9, 20,.12)]'
                    : v.usable ? 'border-ink-700 bg-ink-850 hover:border-ink-600'
                        : 'border-ink-700 bg-ink-900 opacity-70') + '">' +
            '<button data-voucher-pick="' + CB.escape(v.code) + '"' + (v.usable ? '' : ' disabled=""') +
            ' aria-pressed="' + active + '" class="flex min-w-0 flex-1 items-center gap-3.5 p-3.5 text-left disabled:cursor-not-allowed">' +
            '<span class="' + cx('flex h-10 w-10 shrink-0 items-center justify-center rounded-lg',
                active ? 'bg-brand-500 text-on-brand' : v.usable ? 'bg-brand-950 text-brand-500' : 'bg-ink-800 text-ink-500') + '">' +
            (active ? CB.icon('check', 17) : CB.icon('tag', 16)) + '</span>' +
            '<span class="min-w-0 flex-1"><span class="flex flex-wrap items-baseline gap-x-2">' +
            '<span class="truncate text-sm font-semibold">' + CB.escape(v.name) + '</span>' +
            '<code class="font-mono text-[11px] tracking-wider text-faint">' + CB.escape(v.code) + '</code></span>' +
            (v.usable
                ? '<span class="mt-0.5 block text-[13px] font-semibold text-mint-500">Giảm ' + CB.money(v.discountAmount) + '</span>'
                : '<span class="mt-0.5 flex items-start gap-1 text-[12px] text-faint">' + CB.icon('info', 12, 'mt-0.5 shrink-0') + ' ' + CB.escape(v.reason) + '</span>') +
            '</span></button>' +
            '<button data-voucher-save="' + v.id + '" data-saved="' + v.saved + '" title="' + savedLabel + '" aria-label="' + savedLabel + '" class="' +
            cx('flex w-11 shrink-0 items-center justify-center border-l transition-colors',
                v.saved ? 'border-brand-600/30 text-brand-500 hover:bg-brand-950'
                    : 'border-ink-700 text-faint hover:bg-ink-800 hover:text-brand-500') + '">' +
            (v.saved ? CB.icon('ticket', 15) : CB.icon('bookmark-plus', 15)) + '</button></div>';
    }

    function voucherGroup(icon, title, list) {
        return '<div><p class="mb-2.5 flex items-center gap-1.5 text-[12px] font-semibold uppercase tracking-wide text-faint">' +
            icon + ' ' + CB.escape(title) + '</p><div class="flex flex-col gap-2.5">' + list.map(voucherRow).join('') + '</div></div>';
    }

    function voucherInput() {
        if (!voucher.showInput) {
            return '<button data-voucher-show class="flex items-center gap-1.5 self-start text-[13px] text-brand-500 transition-colors hover:underline">' +
                CB.icon('plus', 14) + ' Tôi có mã khác</button>';
        }
        return '<div class="flex flex-col gap-2"><div class="flex gap-2">' +
            '<input data-voucher-input value="' + CB.escape(voucher.manual) + '" placeholder="Nhập mã, ví dụ CHAOBAN" class="' +
            'w-full rounded-xl border border-ink-600 bg-ink-900 px-3.5 py-2.5 text-sm text-cream placeholder:text-faint transition-colors focus:border-brand-600 focus:outline-none disabled:opacity-50 font-mono uppercase">' +
            '<button data-voucher-add class="inline-flex items-center justify-center transition-all duration-150 disabled:opacity-45 disabled:cursor-not-allowed active:scale-[.985] bg-ink-800 text-cream border border-ink-600 hover:border-ink-500 hover:bg-ink-700 h-11 px-5 text-sm rounded-xl gap-2"' +
            (voucher.manual.trim() ? '' : ' disabled=""') + '>Thêm</button>' +
            '<button data-voucher-hide class="inline-flex items-center justify-center transition-all duration-150 disabled:opacity-45 disabled:cursor-not-allowed active:scale-[.985] text-muted hover:text-cream hover:bg-ink-800 h-11 px-5 text-sm rounded-xl gap-2">' +
            CB.icon('x', 15) + '</button></div>' +
            '<div data-voucher-error class="rounded-xl border border-velvet-500/35 bg-velvet-950 px-3.5 py-3 text-[13px] text-velvet-400"' +
            (voucher.error ? '' : ' style="display:none"') + '>' + CB.escape(voucher.error || '') + '</div></div>';
    }

    function renderVouchers() {
        var list = voucher.cache[voucher.amount] || [];
        var saved = list.filter(function (v) { return v.saved; });
        var others = list.filter(function (v) { return !v.saved; });
        var html = '<div class="flex flex-col gap-4">';
        if (list.length === 0) {
            html += '<p class="rounded-xl border border-dashed border-ink-700 px-4 py-6 text-center text-[13px] text-faint">Chưa có voucher nào khả dụng.</p>';
        }
        if (saved.length > 0) html += voucherGroup(CB.icon('ticket', 13), 'Kho voucher của bạn (' + saved.length + ')', saved);
        if (others.length > 0) html += voucherGroup(CB.icon('tag', 13), 'Ưu đãi đang chạy', others);
        html += voucherInput() + '</div>';
        $('#voucher-picker').html(html);
    }

    function saveByCode() {
        var code = voucher.manual;
        if (!code.trim()) return;
        var $btn = $('[data-voucher-add]');
        CB.setLoading($btn, true);
        $.post('/Booking/SaveVoucherByCode', { code: code, __RequestVerificationToken: token }, function (r) {
            if (r.ok) {
                voucher.error = null;
                voucher.manual = '';
                voucher.showInput = false;
                state.promo = code.trim().toUpperCase();
                renderVouchers();
                invalidateVouchers();
            } else {
                CB.setLoading($btn, false);
                voucher.error = r.message;
                $('[data-voucher-error]').text(r.message).show();
            }
        });
    }

    $(document).on('click', '[data-voucher-pick]', function () {
        var code = $(this).attr('data-voucher-pick');
        state.promo = code === state.promo ? '' : code;
        renderVouchers();
    });

    $(document).on('click', '[data-voucher-save]', function () {
        var id = $(this).attr('data-voucher-save');
        var url = $(this).attr('data-saved') === 'true' ? '/Booking/UnsaveVoucher/' : '/Booking/SaveVoucher/';
        $.post(url + id, { __RequestVerificationToken: token }, function () { invalidateVouchers(); });
    });

    $(document).on('click', '[data-voucher-show]', function () {
        voucher.showInput = true;
        renderVouchers();
        $('[data-voucher-input]').trigger('focus');
    });

    $(document).on('click', '[data-voucher-hide]', function () {
        voucher.showInput = false;
        voucher.error = null;
        renderVouchers();
    });

    $(document).on('input', '[data-voucher-input]', function () {
        var upper = this.value.toUpperCase();
        if (upper !== this.value) {
            var pos = this.selectionStart;
            this.value = upper;
            this.setSelectionRange(pos, pos);
        }
        voucher.manual = upper;
        voucher.error = null;
        $('[data-voucher-error]').empty().hide();
        $('[data-voucher-add]').prop('disabled', !upper.trim());
    });

    $(document).on('keydown', '[data-voucher-input]', function (e) {
        if (e.key === 'Enter' && voucher.manual.trim()) saveByCode();
    });

    $(document).on('click', '[data-voucher-add]', saveByCode);

    renderSteps();
    loadSeatMap();
    setStep(state.step);

    function initConfirm() {
        var $confirm = $('[data-confirm]');
        var provider = $confirm.attr('data-provider');
        var method = $confirm.attr('data-method');

        function syncForm() {
            $('#pay-form [name=Provider]').val(provider);
            $('#pay-form [name=MethodCode]').val(method);
        }

        function renderProviders() {
            $('[data-provider-option]').each(function () {
                var on = $(this).attr('data-provider-option') === provider;
                $(this).attr('aria-pressed', String(on)).attr('class', cx(
                    'flex items-center gap-3 rounded-xl border px-4 py-3 text-left transition-all',
                    on ? 'border-brand-500 bg-brand-950/60' : 'border-ink-600 bg-ink-900 hover:border-ink-500'));
                $(this).find('[data-provider-icon]').attr('class', cx(
                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
                    on ? 'bg-brand-500 text-on-brand' : 'bg-ink-800 text-muted'));
                $(this).find('[data-provider-check]').css('display', on ? 'contents' : 'none');
            });
            $('[data-methods]').each(function () {
                var visible = $(this).attr('data-methods') === provider;
                $(this).toggle(visible);
                if (!visible) return;
                $(this).find('[data-method-option]').each(function () {
                    var on = $(this).attr('data-method-option') === method;
                    $(this).attr('aria-pressed', String(on)).attr('class', cx(
                        'flex items-center gap-2 rounded-xl border px-3.5 py-2 text-[13px] transition-all',
                        on ? 'border-brand-500 bg-brand-500 font-semibold text-on-brand'
                            : 'border-ink-600 bg-ink-900 text-muted hover:border-ink-500 hover:text-cream'));
                });
                $(this).find('[data-qr-note]').toggle(method === 'VNPAYQR');
            });
            syncForm();
        }

        $('[data-provider-option]').on('click', function () {
            provider = $(this).attr('data-provider-option');
            var $first = $('[data-methods="' + provider + '"] [data-method-option]').first();
            method = $first.length ? $first.attr('data-method-option') : '';
            renderProviders();
        });

        $('[data-method-option]').on('click', function () {
            method = $(this).attr('data-method-option');
            renderProviders();
        });

        $('#pay-form').on('submit', function () {
            syncForm();
            CB.setLoading($('[data-pay]'), true);
            $('[data-abort]').prop('disabled', true);
        });

        $('#cancel-hold-form').on('submit', function () {
            CB.setLoading($('[data-cancel-hold]'), true);
            $('[data-abort]').prop('disabled', true);
        });

        syncForm();

        var $countdown = $('[data-countdown]');
        var expires = new Date($countdown.attr('data-countdown')).getTime();
        var expiredUrl = $countdown.attr('data-expired-url');

        function secondsLeft() {
            return Math.max(0, Math.floor((expires - Date.now()) / 1000));
        }

        function tick() {
            var left = secondsLeft();
            var urgent = left <= 60;
            $countdown.attr('class', cx('flex items-center gap-2 rounded-xl border px-3.5 py-2',
                urgent ? 'border-velvet-500/45 bg-velvet-950' : 'border-brand-600/40 bg-brand-950'));
            $countdown.children('svg').attr('class', 'lucide lucide-timer ' + (urgent ? 'text-velvet-400' : 'text-brand-400'));
            $countdown.find('[data-countdown-text]').attr('class', cx('font-mono text-lg font-bold tabular',
                urgent ? 'text-velvet-400' : 'text-brand-400')).text(CB.countdown(left));
            return left;
        }

        tick();
        var timer = setInterval(function () {
            if (tick() <= 0) {
                clearInterval(timer);
                location.replace(expiredUrl);
            }
        }, 1000);
    }
});
