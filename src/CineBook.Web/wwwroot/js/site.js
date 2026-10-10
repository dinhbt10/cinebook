window.CB = (function ($) {
    function icon(name, size, cls, filled) {
        var data = window.CB_ICONS[name];
        if (!data) return '';
        size = size || 24;
        var css = data.cls + (cls ? ' ' + cls : '');
        return '<svg xmlns="http://www.w3.org/2000/svg" width="' + size + '" height="' + size +
            '" viewBox="0 0 24 24" fill="' + (filled ? 'currentColor' : 'none') +
            '" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="' +
            css + '" aria-hidden="true">' + data.inner + '</svg>';
    }

    function escape(text) {
        return $('<div>').text(text == null ? '' : String(text)).html();
    }

    var VN_OFFSET_MS = 7 * 60 * 60 * 1000;

    function pad(n) { return String(n).padStart(2, '0'); }

    function toVN(utc) { return new Date(new Date(utc).getTime() + VN_OFFSET_MS); }

    function money(v) { return Math.round(v).toLocaleString('vi-VN') + 'đ'; }

    function time(utc) {
        var d = toVN(utc);
        return pad(d.getUTCHours()) + ':' + pad(d.getUTCMinutes());
    }

    function fullDateTime(utc) {
        var d = toVN(utc);
        return time(utc) + ' · ' + pad(d.getUTCDate()) + '/' + pad(d.getUTCMonth() + 1) + '/' + d.getUTCFullYear();
    }

    function countdown(seconds) {
        var s = Math.max(0, seconds);
        return Math.floor(s / 60) + ':' + pad(s % 60);
    }

    function setLoading(button, loading) {
        var $b = $(button);
        if (loading) {
            if ($b.data('cb-loading')) return;
            $b.data('cb-loading', true).prop('disabled', true);
            $b.prepend($(icon('loader-2', 15, 'animate-spin')).attr('data-spinner', ''));
        } else {
            $b.data('cb-loading', false).prop('disabled', false);
            $b.find('[data-spinner]').remove();
        }
    }

    var openModals = [];

    function openModal(modal) {
        var $m = $(modal);
        $m.css('display', 'flex');
        openModals.push($m[0]);
        $m.find('[autofocus]').first().trigger('focus');
    }

    function closeModal(modal) {
        var $m = $(modal);
        if ($m.data('busy')) return;
        $m.css('display', 'none');
        openModals = openModals.filter(function (m) { return m !== $m[0]; });
        $m.trigger('cb:closed');
    }

    $(document).on('click', '[data-modal]', function (e) {
        if (e.target === this) closeModal(this);
    });
    $(document).on('click', '[data-modal-close]', function () {
        closeModal($(this).closest('[data-modal]'));
    });
    $(document).on('click', '[data-modal-open]', function (e) {
        e.preventDefault();
        openModal($($(this).data('modal-open')));
    });
    $(document).on('keydown', function (e) {
        if (e.key === 'Escape' && openModals.length) closeModal(openModals[openModals.length - 1]);
    });

    $(document).on('click', '[data-confirm-submit]', function () {
        var $dialog = $(this).closest('[data-modal]');
        var form = $dialog.data('form');
        if (!form) return;
        $dialog.data('busy', true);
        $dialog.find('[data-modal-close]').prop('disabled', true);
        setLoading(this, true);
        $(form).trigger('submit');
    });

    $(document).on('submit', 'form[data-loading]', function () {
        var $form = $(this);
        if ($.fn.valid && $form.data('validator') && !$form.valid()) return;
        setLoading($form.find('[type=submit]').first(), true);
    });

    $(document).on('click', '[data-theme-toggle]', function () {
        var root = document.documentElement;
        var next = root.dataset.theme === 'dark' ? 'light' : 'dark';
        root.dataset.theme = next;
        root.classList.add('theme-switching');
        setTimeout(function () { root.classList.remove('theme-switching'); }, 260);
        document.cookie = 'theme=' + next + '; path=/; max-age=31536000; samesite=lax';

        var dark = next === 'dark';
        var label = dark ? 'Chuyển sang nền sáng' : 'Chuyển sang nền tối';
        $('[data-theme-toggle]').each(function () {
            var $t = $(this).attr({ title: label, 'aria-label': label });
            var $sun = $t.find('.lucide-sun'), $moon = $t.find('.lucide-moon');
            $sun.removeClass('scale-0 -rotate-90 opacity-0 scale-100 rotate-0 opacity-100')
                .addClass(dark ? 'scale-0 -rotate-90 opacity-0' : 'scale-100 rotate-0 opacity-100');
            $moon.removeClass('scale-100 rotate-0 opacity-100 scale-0 rotate-90 opacity-0')
                .addClass(dark ? 'scale-100 rotate-0 opacity-100' : 'scale-0 rotate-90 opacity-0');
        });
    });

    $(document).on('click', '[data-menu-toggle]', function () {
        var $menu = $('[data-menu]');
        var open = $menu.hasClass('hidden');
        $menu.toggleClass('hidden', !open);
        $('[data-menu-icon=closed]').toggleClass('hidden', open);
        $('[data-menu-icon=open]').toggleClass('hidden', !open);
    });

    $(document).on('click', '[data-dismiss]', function () {
        $(this).closest($(this).data('dismiss')).remove();
    });

    function youtubeId(url) {
        var m = url.match(/(?:youtu\.be\/|v=|\/embed\/|\/shorts\/)([\w-]{11})/);
        return m ? m[1] : null;
    }

    function openTrailer(title, url) {
        var $modal = $('#trailer-modal');
        if (!$modal.length || !url) return;
        var id = youtubeId(url);
        $modal.find('[data-trailer-title]').text(title);
        var $body = $modal.find('[data-trailer-body]').empty();
        if (id) {
            $body.append($('<iframe>', {
                'class': 'h-full w-full',
                src: 'https://www.youtube-nocookie.com/embed/' + id + '?autoplay=1&rel=0',
                title: 'Trailer ' + title,
                allow: 'accelerometer; autoplay; clipboard-write; encrypted-media; picture-in-picture',
                allowfullscreen: true
            }));
        } else {
            $body.append(
                '<div class="flex h-full flex-col items-center justify-center gap-3 text-sm text-faint">' +
                '<p>Không nhúng được trailer này.</p>' +
                '<a href="' + escape(url) + '" target="_blank" rel="noreferrer" class="text-brand-400 underline underline-offset-4">Mở ở tab mới</a></div>');
        }
        $modal.attr('aria-label', 'Trailer ' + title);
        openModal($modal);
        document.body.style.overflow = 'hidden';
    }

    $(document).on('cb:closed', '#trailer-modal', function () {
        $(this).find('[data-trailer-body]').empty();
        document.body.style.overflow = '';
    });

    $(document).on('click', '[data-trailer]', function (e) {
        e.preventDefault();
        openTrailer($(this).data('title'), $(this).data('trailer'));
    });

    return {
        icon: icon,
        escape: escape,
        money: money,
        time: time,
        fullDateTime: fullDateTime,
        countdown: countdown,
        setLoading: setLoading,
        openModal: openModal,
        closeModal: closeModal,
        openTrailer: openTrailer
    };
})(jQuery);
