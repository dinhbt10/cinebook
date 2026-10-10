window.TicketImage = (function () {
    var W = 420;
    var H = 620;
    var SCALE = 3;

    var BRAND = '#E50914';
    var INK = '#141414';
    var MUTED = '#6B6B73';
    var LINE = '#E2E2E6';

    function svgToImage(svg) {
        var xml = new XMLSerializer().serializeToString(svg);
        var url = URL.createObjectURL(new Blob([xml], { type: 'image/svg+xml;charset=utf-8' }));
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { URL.revokeObjectURL(url); resolve(img); };
            img.onerror = function (e) { URL.revokeObjectURL(url); reject(e); };
            img.src = url;
        });
    }

    function fit(ctx, text, maxWidth) {
        if (ctx.measureText(text).width <= maxWidth) return text;
        var cut = text;
        while (cut.length > 1 && ctx.measureText(cut + '…').width > maxWidth) cut = cut.slice(0, -1);
        return cut + '…';
    }

    function line(ctx, label, value, y) {
        ctx.font = '12px "Be Vietnam Pro", system-ui, sans-serif';
        ctx.fillStyle = MUTED;
        ctx.textAlign = 'left';
        ctx.fillText(label, 28, y);

        ctx.font = '600 13px "Be Vietnam Pro", system-ui, sans-serif';
        ctx.fillStyle = INK;
        ctx.textAlign = 'right';
        ctx.fillText(fit(ctx, value, 250), W - 28, y);
    }

    function draw(ctx, img, ticket, booking) {
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, W, H);
        ctx.fillStyle = BRAND;
        ctx.fillRect(0, 0, W, 6);

        ctx.textBaseline = 'alphabetic';

        ctx.font = '800 19px Bitter, Georgia, serif';
        ctx.fillStyle = INK;
        ctx.textAlign = 'left';
        ctx.fillText('CineBook', 28, 44);

        ctx.font = '11px "Be Vietnam Pro", system-ui, sans-serif';
        ctx.fillStyle = MUTED;
        ctx.textAlign = 'right';
        ctx.fillText('VÉ ĐIỆN TỬ', W - 28, 44);

        ctx.font = '800 21px Bitter, Georgia, serif';
        ctx.fillStyle = INK;
        ctx.textAlign = 'left';
        ctx.fillText(fit(ctx, booking.movieTitle, W - 56), 28, 82);

        ctx.font = '12px "Be Vietnam Pro", system-ui, sans-serif';
        ctx.fillStyle = MUTED;
        ctx.fillText(fit(ctx, booking.cinemaName + ' · ' + booking.roomName, W - 56), 28, 103);

        var qr = 210;
        ctx.drawImage(img, (W - qr) / 2, 126, qr, qr);

        ctx.textAlign = 'center';
        ctx.font = '800 30px Bitter, Georgia, serif';
        ctx.fillStyle = INK;
        ctx.fillText(ticket.seatLabel, W / 2, 380);

        ctx.font = '11px "JetBrains Mono", ui-monospace, monospace';
        ctx.fillStyle = MUTED;
        ctx.fillText(ticket.code, W / 2, 400);

        ctx.strokeStyle = LINE;
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 5]);
        ctx.beginPath();
        ctx.moveTo(20, 424);
        ctx.lineTo(W - 20, 424);
        ctx.stroke();
        ctx.setLineDash([]);

        ctx.fillStyle = '#FFFFFF';
        [0, W].forEach(function (cx) {
            ctx.beginPath();
            ctx.arc(cx, 424, 9, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = LINE;
            ctx.stroke();
        });

        line(ctx, 'Suất chiếu', CB.fullDateTime(booking.startsAtUtc), 456);
        line(ctx, 'Mã đặt vé', booking.code, 482);
        line(ctx, 'Ghế', ticket.seatLabel + ' (' + booking.seatCount + ' vé trong đơn)', 508);
        line(ctx, 'Tổng đơn', CB.money(booking.total), 534);

        ctx.textAlign = 'center';
        ctx.font = '11px "Be Vietnam Pro", system-ui, sans-serif';
        ctx.fillStyle = MUTED;
        ctx.fillText('Xuất trình mã QR này tại cửa phòng chiếu', W / 2, 578);

        ctx.strokeStyle = LINE;
        ctx.strokeRect(0.5, 0.5, W - 1, H - 1);
    }

    function build(svg, ticket, booking) {
        var fontsReady = document.fonts && document.fonts.ready ? document.fonts.ready.catch(function () {}) : Promise.resolve();
        return fontsReady.then(function () {
            var canvas = document.createElement('canvas');
            canvas.width = W * SCALE;
            canvas.height = H * SCALE;
            var ctx = canvas.getContext('2d');
            if (!ctx) throw new Error('Trình duyệt không hỗ trợ canvas.');
            ctx.scale(SCALE, SCALE);

            return svgToImage(svg).then(function (img) {
                draw(ctx, img, ticket, booking);
                return new Promise(function (resolve, reject) {
                    canvas.toBlob(function (b) {
                        if (b) resolve(b); else reject(new Error('Không tạo được ảnh.'));
                    }, 'image/png');
                });
            });
        });
    }

    function save(blob, filename) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

    function fileName(booking, ticket) {
        return 'CineBook-' + booking.code + '-' + ticket.seatLabel + '.png';
    }

    return { build: build, save: save, fileName: fileName };
})();
