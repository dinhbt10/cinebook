$(function () {
    var VN_OFFSET_MS = 7 * 60 * 60 * 1000;
    var token = $('input[name=__RequestVerificationToken]').first().val();
    var $list = $('#showtime-list');
    var $date = $('#filter-date');
    var $cinema = $('#filter-cinema');
    var $form = $('form[data-showtime-form]');
    var $room = $form.find('#RoomId');
    var $overrides = $form.find('#PriceOverrides');
    var $restore = $form.find('[data-restore-prices]');
    var suggested = null;
    var previewSeq = 0;
    var overrides = {};

    function utcFromVnInput(local) {
        var parts = local.split('T');
        var d = parts[0].split('-').map(Number);
        var t = (parts[1] || '00:00').split(':').map(Number);
        return new Date(Date.UTC(d[0], d[1] - 1, d[2], t[0], t[1]) - VN_OFFSET_MS).toISOString();
    }

    function showError(message) {
        var $box = $('[data-page-messages]');
        var $note = $box.find('[data-page-error]');
        if (!$note.length) {
            $note = $('<div class="mb-4" data-page-error><div class="rounded-xl border border-velvet-500/35 bg-velvet-950 px-3.5 py-3 text-[13px] text-velvet-400"></div></div>');
            $box.prepend($note);
        }
        $note.children().text(message);
    }

    function reloadList(showSpinner) {
        var date = $date.val();
        if (!date) return;
        var cinemaId = $cinema.val();
        if (showSpinner) {
            $list.html('<div class="flex flex-col items-center justify-center gap-3 py-16 text-faint">' +
                CB.icon('loader-2', 26, 'animate-spin text-brand-500') + '</div>');
        }
        $.ajax({
            url: '/Admin/Showtimes/List',
            data: { date: date, cinemaId: cinemaId },
            success: function (response) {
                $list.html(response);
            }
        });
    }

    function syncFilters() {
        var date = $date.val();
        var cinemaId = $cinema.val();
        $('input[name=FilterDate]').val(date);
        $('input[name=FilterCinemaId]').val(cinemaId);
        var query = '?date=' + encodeURIComponent(date) + (cinemaId ? '&cinemaId=' + cinemaId : '');
        window.history.replaceState(null, '', window.location.pathname + query);
    }

    $date.add($cinema).on('change', function () {
        syncFilters();
        reloadList(true);
    });

    $('[data-showtime-create]').on('click', function () {
        CB.openModal('#create-showtime-modal');
    });

    $form.find('#CinemaId').on('change', function () {
        var cinemaId = $(this).val();
        $room.html('<option value="">— Chọn phòng —</option>').prop('disabled', !cinemaId);
        refreshPreview();
        if (!cinemaId) return;
        $.getJSON('/Admin/Showtimes/Rooms', { cinemaId: cinemaId }, function (rooms) {
            if ($form.find('#CinemaId').val() !== cinemaId) return;
            $.each(rooms, function (i, r) {
                $room.append($('<option>').val(r.id).text(r.label));
            });
        });
    });

    function saveOverrides() {
        var keys = Object.keys(overrides);
        $overrides.val(keys.join(','));
        $restore.toggle(keys.length > 0);
    }

    function fillPrices() {
        $form.find('[data-price]').each(function () {
            var key = $(this).data('price');
            if (overrides[key]) return;
            $(this).val(suggested ? suggested[key] : '');
        });
    }

    function refreshPreview() {
        var roomId = $room.val();
        var starts = $form.find('#StartsLocal').val();
        var basePrice = Number($form.find('#BasePrice').val());
        var seq = ++previewSeq;
        suggested = null;
        if (!roomId || !starts || !(basePrice > 0)) {
            fillPrices();
            return;
        }
        $.getJSON('/Admin/Showtimes/PricePreview', {
            roomId: roomId,
            startsAtUtc: utcFromVnInput(starts),
            basePrice: basePrice
        }, function (prices) {
            if (seq !== previewSeq) return;
            suggested = prices;
            fillPrices();
        });
    }

    $room.on('change', refreshPreview);
    $form.find('#StartsLocal, #BasePrice').on('input change', refreshPreview);

    $form.find('[data-price]').on('input', function () {
        var key = $(this).data('price');
        if ($(this).val() === '') delete overrides[key];
        else overrides[key] = true;
        saveOverrides();
    });

    $restore.on('click', function () {
        overrides = {};
        saveOverrides();
        fillPrices();
    });

    $.each(($overrides.val() || '').split(','), function (i, key) {
        if (key) overrides[key] = true;
    });
    saveOverrides();
    if ($room.val() && $form.find('#StartsLocal').val()) {
        refreshPreview();
    }

    $(document).on('click', '[data-toggle-sale]', function () {
        var $btn = $(this);
        $.post('/Admin/Showtimes/SetOnSale', {
            id: $btn.data('toggle-sale'),
            value: $btn.data('value'),
            __RequestVerificationToken: token
        }, function (res) {
            if (!res.ok) {
                showError(res.message);
                return;
            }
            reloadList(false);
        });
    });

    var $cancel = $('#cancel-showtime-dialog');
    $(document).on('click', '[data-cancel-showtime]', function () {
        var $btn = $(this);
        var $cancelForm = $cancel.find('form');
        var validator = $cancelForm.data('validator');
        if (validator) validator.resetForm();
        $cancelForm.find('.input-validation-error').removeClass('input-validation-error');
        $cancelForm.find('[data-valmsg-for]').empty().removeClass('field-validation-error').addClass('field-validation-valid');
        $cancelForm.find('#Id').val($btn.data('cancel-showtime'));
        $cancelForm.find('#Reason').val('Sự cố kỹ thuật');
        $cancel.find('[data-cancel-title]').text('Hủy suất ' + $btn.data('time') + ' — ' + $btn.data('movie') + '?');
        $cancel.find('[data-cancel-sold]').text($btn.data('sold') + ' ghế đã bán/đang giữ sẽ được hoàn 100%. Không hoàn tác được.');
        CB.openModal($cancel);
        $cancelForm.find('#Reason').trigger('select');
    });
});
