$(function () {
    var $modal = $('#movie-modal');
    var $host = $('#movie-form-host');
    var editor = null;

    function destroyEditor() {
        if (editor) {
            editor.destroy().catch(function () { });
            editor = null;
        }
    }

    function initForm() {
        var $form = $host.find('form[data-movie-form]');
        if ($.validator && $.validator.unobtrusive) {
            $.validator.unobtrusive.parse($form);
        }

        var textarea = $form.find('#Description')[0];
        if (textarea && window.ClassicEditor) {
            ClassicEditor.create(textarea, {
                language: 'vi',
                toolbar: ['heading', '|', 'bold', 'italic', 'link', 'bulletedList', 'numberedList', 'blockQuote', '|', 'undo', 'redo'],
                heading: {
                    options: [
                        { model: 'paragraph', title: 'Đoạn văn', class: 'ck-heading_paragraph' },
                        { model: 'heading2', view: 'h2', title: 'Tiêu đề lớn', class: 'ck-heading_heading2' },
                        { model: 'heading3', view: 'h3', title: 'Tiêu đề nhỏ', class: 'ck-heading_heading3' }
                    ]
                }
            }).then(function (created) {
                editor = created;
            });
        }
    }

    function showForm(html) {
        destroyEditor();
        $host.html(html);
        initForm();
    }

    function load(url) {
        $.ajax({
            url: url,
            success: function (response) {
                showForm(response);
                CB.openModal($modal);
            },
            error: function () {
                alertError('Không tìm thấy dữ liệu.');
            }
        });
    }

    function alertError(message) {
        var $note = $('[data-page-error]');
        if (!$note.length) {
            $note = $('<div class="mb-4" data-page-error><div class="rounded-xl border border-velvet-500/35 bg-velvet-950 px-3.5 py-3 text-[13px] text-velvet-400"></div></div>');
            $('h1').first().parent().after($note);
        }
        $note.children().text(message);
    }

    $('[data-movie-create]').on('click', function () {
        load('/Admin/Movies/Create');
    });

    $(document).on('click', '[data-movie-edit]', function () {
        load('/Admin/Movies/Edit/' + $(this).data('movie-edit'));
    });

    $(document).on('submit', 'form[data-movie-form]', function (e) {
        e.preventDefault();
        var form = this;
        var $form = $(form);
        if (editor) {
            editor.updateSourceElement();
        }
        if ($.fn.valid && $form.data('validator') && !$form.valid()) {
            return;
        }

        var $submit = $form.find('[type=submit]').first();
        CB.setLoading($submit, true);
        $modal.data('busy', true);

        $.ajax({
            url: form.action,
            type: 'POST',
            data: new FormData(form),
            processData: false,
            contentType: false,
            success: function (response) {
                if (response && response.redirect) {
                    window.location.href = response.redirect;
                    return;
                }
                $modal.data('busy', false);
                showForm(response);
                $modal.scrollTop(0);
            },
            error: function () {
                $modal.data('busy', false);
                CB.setLoading($submit, false);
                alertError('Không lưu được phim, vui lòng thử lại.');
            }
        });
    });

    $modal.on('cb:closed', function () {
        destroyEditor();
        $host.empty();
    });

    $(document).on('click', '[data-movie-delete]', function () {
        var $btn = $(this);
        var $dialog = $('#delete-dialog');
        $('#delete-form input[name=id]').val($btn.data('movie-delete'));
        $dialog.find('[data-confirm-body]').text(
            'Xoá phim "' + $btn.data('title') + '"? Phim đã có suất chiếu sẽ chỉ chuyển sang Đã kết thúc.');
        CB.openModal($dialog);
    });
});
