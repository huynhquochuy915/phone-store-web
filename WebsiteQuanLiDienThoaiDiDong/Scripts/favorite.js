(function ($) {
    $(function () {
        var buttons = Array.prototype.slice.call(document.querySelectorAll('.favorite-toggle'));
        if (!buttons.length) return;

        function setState(button, saved) {
            button.setAttribute('aria-pressed', saved ? 'true' : 'false');
            button.setAttribute('aria-label', saved ? 'Bỏ yêu thích' : 'Thêm vào yêu thích');
            button.title = saved ? 'Bỏ yêu thích' : 'Thêm vào yêu thích';
            var icon = button.querySelector('i');
            if (icon) icon.className = 'bi ' + (saved ? 'bi-heart-fill' : 'bi-heart');
            var label = button.querySelector('.favorite-label');
            if (label) label.textContent = saved ? 'Bỏ khỏi yêu thích' : 'Lưu vào yêu thích';
            button.classList.toggle('is-favorite', saved);
        }

        var statusUrl = buttons[0].getAttribute('data-status-url');
        if (statusUrl) {
            $.getJSON(statusUrl).done(function (ids) {
                var saved = {};
                (ids || []).forEach(function (id) { saved[String(id)] = true; });
                buttons.forEach(function (button) {
                    var id = button.closest('form').querySelector('[name="sanPhamId"]').value;
                    setState(button, !!saved[id]);
                });
            });
        }

        document.addEventListener('click', function (event) {
            var button = event.target.closest('.favorite-toggle');
            if (!button || button.disabled) return;
            event.preventDefault();
            var form = button.closest('form');
            var token = form.querySelector('[name="__RequestVerificationToken"]');
            var productId = form.querySelector('[name="sanPhamId"]');
            button.disabled = true;

            $.ajax({
                url: form.action,
                method: 'POST',
                dataType: 'json',
                data: {
                    __RequestVerificationToken: token ? token.value : '',
                    sanPhamId: productId ? productId.value : ''
                }
            }).done(function (result) {
                if (result.requiresLogin) {
                    window.location.href = result.loginUrl;
                    return;
                }
                if (!result.ok) {
                    if (typeof window.showToast === 'function') window.showToast(result.message || 'Không thể cập nhật yêu thích.', false);
                    return;
                }
                setState(button, result.saved);
                if (document.querySelector('[data-favorite-page="true"]')) {
                    window.location.reload();
                    return;
                }
                if (typeof window.showToast === 'function') window.showToast(result.message, true);
            }).fail(function (xhr) {
                var message = xhr.status === 400
                    ? 'Phiên làm việc đã hết hạn. Hãy tải lại trang rồi thử lại.'
                    : 'Không cập nhật được yêu thích. Vui lòng thử lại.';
                if (typeof window.showToast === 'function') window.showToast(message, false);
            }).always(function () {
                button.disabled = false;
            });
        });
    });
})(window.jQuery);
