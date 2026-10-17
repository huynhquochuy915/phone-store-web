(function ($) {
    $(function () {
        var form = document.querySelector('.site-header form.search');
        if (!form) return;

        var input = form.querySelector('input[name="tuKhoa"]');
        if (!input) return;

        var indexUrl = new URL(form.action, window.location.href);
        var productPath = indexUrl.pathname.replace(/\/Index\/?$/i, '');
        var suggestUrl = productPath + '/GoiY';
        var detailUrl = productPath + '/ChiTiet/';
        var menu = document.createElement('div');
        menu.className = 'list-group search-suggestions';
        menu.style.cssText = 'display:none;position:absolute;left:0;right:0;top:calc(100% + 4px);z-index:1080;max-height:360px;overflow-y:auto;background:#fff;box-shadow:0 8px 24px rgba(0,0,0,.16);border-radius:8px';
        form.appendChild(menu);

        var timer = null;
        var request = null;

        function hide() {
            menu.style.display = 'none';
            menu.innerHTML = '';
        }

        function show(items) {
            menu.innerHTML = '';
            items.forEach(function (item) {
                var link = document.createElement('a');
                link.className = 'list-group-item list-group-item-action d-flex align-items-center gap-2';
                link.href = detailUrl + encodeURIComponent(item.id);

                if (item.anh) {
                    var image = document.createElement('img');
                    image.src = item.anh.charAt(0) === '/' ? item.anh : '/' + item.anh;
                    image.alt = '';
                    image.style.cssText = 'width:44px;height:44px;object-fit:contain;flex:none';
                    image.onerror = function () { this.style.display = 'none'; };
                    link.appendChild(image);
                }

                var info = document.createElement('span');
                info.className = 'flex-grow-1';
                var name = document.createElement('span');
                name.className = 'd-block fw-semibold';
                name.textContent = item.ten || '';
                info.appendChild(name);
                if (item.thuongHieu) {
                    var brand = document.createElement('small');
                    brand.className = 'text-muted';
                    brand.textContent = item.thuongHieu;
                    info.appendChild(brand);
                }
                link.appendChild(info);

                if (item.gia != null) {
                    var price = document.createElement('span');
                    price.className = 'text-danger fw-semibold';
                    price.textContent = Number(item.gia).toLocaleString('vi-VN') + '₫';
                    link.appendChild(price);
                }
                menu.appendChild(link);
            });
            menu.style.display = items.length ? 'block' : 'none';
        }

        input.addEventListener('input', function () {
            window.clearTimeout(timer);
            var keyword = input.value.trim();
            if (keyword.length < 2) { hide(); return; }

            timer = window.setTimeout(function () {
                if (request && request.readyState !== 4) request.abort();
                request = $.getJSON(suggestUrl, { tuKhoa: keyword })
                    .done(show)
                    .fail(function (_, status) { if (status !== 'abort') hide(); });
            }, 250);
        });

        input.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') hide();
        });
        document.addEventListener('click', function (event) {
            if (!form.contains(event.target)) hide();
        });
        form.addEventListener('submit', function () { hide(); });
    });
})(window.jQuery);
