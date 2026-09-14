// Modal + fragment refresh conventions. See Infrastructure/ModalControllerExtensions.cs for the server side.
//
//   [data-modal-url]        clicking loads the URL (a partial view) into the shared modal
//   form[data-modal-form]   submits with fetch; the response decides what happens next:
//       X-Modal-Result: refresh   -> close the modal and replace the element at X-Modal-Target with the response HTML
//       X-Modal-Result: redirect  -> close the modal and navigate to X-Modal-Location
//       (no header)               -> re-render the modal with the returned partial (validation errors)
(function () {
    'use strict';

    const modalEl = document.getElementById('app-modal');
    if (!modalEl) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const content = modalEl.querySelector('.modal-content');
    const ajaxHeaders = { 'X-Requested-With': 'XMLHttpRequest' };

    // Bootstrap ignores show()/hide() while a transition is running, so track the lifecycle and defer as needed.
    let state = 'hidden'; // hidden | showing | shown | hiding
    modalEl.addEventListener('show.bs.modal', () => { state = 'showing'; });
    modalEl.addEventListener('shown.bs.modal', () => { state = 'shown'; });
    modalEl.addEventListener('hide.bs.modal', () => { state = 'hiding'; });
    modalEl.addEventListener('hidden.bs.modal', () => { state = 'hidden'; });

    function openModal() {
        if (state === 'hidden') modal.show();
        else if (state === 'hiding') modalEl.addEventListener('hidden.bs.modal', () => modal.show(), { once: true });
        // showing / shown: already on its way, the new content is in place
    }

    function closeModal() {
        if (state === 'shown') modal.hide();
        else if (state === 'showing') modalEl.addEventListener('shown.bs.modal', () => modal.hide(), { once: true });
        // hiding / hidden: nothing to do
    }

    function parseValidation(scope) {
        if (window.jQuery && jQuery.validator && jQuery.validator.unobtrusive) {
            jQuery(scope).find('form').each(function () {
                jQuery(this).removeData('validator').removeData('unobtrusiveValidation');
            });
            jQuery.validator.unobtrusive.parse(scope);
        }
    }

    function focusFirst(scope) {
        const el = scope.querySelector('[autofocus], input:not([type=hidden]):not([disabled]), select, textarea');
        if (el) el.focus();
    }

    function showToast(message, variant) {
        const container = document.getElementById('toast-container');
        if (!container || !message) return;
        const el = document.createElement('div');
        el.className = 'toast align-items-center text-bg-' + (variant || 'success') + ' border-0';
        el.setAttribute('role', 'status');
        el.innerHTML = '<div class="d-flex"><div class="toast-body"></div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        el.querySelector('.toast-body').textContent = message;
        container.appendChild(el);
        const toast = new bootstrap.Toast(el, { delay: 4000 });
        el.addEventListener('hidden.bs.toast', () => el.remove());
        toast.show();
    }

    function renderModal(html) {
        content.innerHTML = html;
        parseValidation(content);
        initReviewForm(content);
        openModal();
        setTimeout(() => focusFirst(content), 250);
    }

    async function loadModal(url) {
        try {
            const res = await fetch(url, { headers: ajaxHeaders, credentials: 'same-origin' });
            if (res.redirected && new URL(res.url).pathname.startsWith('/Account/')) {
                window.location.assign(res.url);
                return;
            }
            if (!res.ok) {
                showToast(res.status === 403 ? 'You are not allowed to do that.' : 'Something went wrong (' + res.status + ').', 'danger');
                return;
            }
            renderModal(await res.text());
        } catch (err) {
            showToast('Could not open the dialog.', 'danger');
        }
    }

    function replaceFragment(selector, html) {
        const target = document.querySelector(selector);
        if (!target) { window.location.reload(); return; }
        const template = document.createElement('template');
        template.innerHTML = html.trim();
        const fresh = template.content.firstElementChild;
        if (!fresh) { window.location.reload(); return; }
        target.replaceWith(fresh);
        parseValidation(fresh);
    }

    async function submitModalForm(form) {
        if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;

        const submitButtons = form.querySelectorAll('button[type=submit]');
        submitButtons.forEach(b => b.disabled = true);
        try {
            const res = await fetch(form.action, {
                method: (form.method || 'post').toUpperCase(),
                body: new FormData(form),
                headers: ajaxHeaders,
                credentials: 'same-origin'
            });
            const result = res.headers.get('X-Modal-Result');
            const message = res.headers.get('X-Modal-Message');

            if (result === 'refresh') {
                const html = await res.text();
                closeModal();
                replaceFragment(res.headers.get('X-Modal-Target'), html);
                if (message) showToast(decodeURIComponent(message));
            } else if (result === 'redirect') {
                closeModal();
                window.location.assign(res.headers.get('X-Modal-Location'));
            } else if (!res.ok) {
                showToast(res.status === 403 ? 'You are not allowed to do that.' : 'Something went wrong (' + res.status + ').', 'danger');
            } else {
                renderModal(await res.text());
            }
        } catch (err) {
            showToast('Could not save your changes.', 'danger');
        } finally {
            submitButtons.forEach(b => b.disabled = false);
        }
    }

    // The review form shows the lease start date only when approving and marks the comment required otherwise.
    function initReviewForm(scope) {
        const form = scope.querySelector('#review-form');
        if (!form) return;
        const lease = form.querySelector('[data-review-lease]');
        const hint = form.querySelector('[data-review-comment-hint]');
        function update() {
            const outcome = form.querySelector('input[name=Outcome]:checked');
            const approving = outcome && outcome.value === 'Approve';
            if (lease) lease.hidden = !approving;
            if (hint) hint.textContent = approving ? '(optional)' : '(required for Return and Deny)';
        }
        form.querySelectorAll('input[name=Outcome]').forEach(r => r.addEventListener('change', update));
        update();
    }

    document.addEventListener('click', function (e) {
        const trigger = e.target.closest('[data-modal-url]');
        if (!trigger) return;
        e.preventDefault();
        loadModal(trigger.getAttribute('data-modal-url'));
    });

    modalEl.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form.matches('form[data-modal-form]')) return;
        e.preventDefault();
        submitModalForm(form);
    });
})();
