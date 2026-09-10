(() => {
    let activeLink;
    let frame;
    const observer = new ResizeObserver(schedulePosition);
    const registrations = new Map();
    const hoverDelay = 250;

    function setRequested(registration, active) {
        if (registration.requested !== active) {
            registration.requested = active;
            registration.callback.invokeMethodAsync('SetPreviewActive', active)
                .catch(error => console.error('Failed to update insertion preview loading state', error));
        }
    }

    function updateLoading(link, immediate = false) {
        const registration = registrations.get(link.querySelector('.insertion-preview').id);
        if (!registration) {
            return;
        }

        const active = link.isConnected && link.matches(':hover, :focus-within') &&
            !link.querySelector('.insertion-preview').classList.contains('dismissed');
        if (!active || immediate) {
            clearTimeout(registration.timer);
            registration.timer = undefined;
        }
        if (!active) {
            setRequested(registration, false);
        } else if (link.dataset.previewLoaded !== 'true' && !registration.requested) {
            if (immediate) {
                setRequested(registration, true);
            } else if (registration.timer === undefined) {
                registration.timer = setTimeout(() => {
                    registration.timer = undefined;
                    updateLoading(link, true);
                }, hoverDelay);
            }
        }
    }

    window.insertionPreview = {
        register(link, callback) {
            registrations.set(link.querySelector('.insertion-preview').id, { link, callback, requested: false });
            if (link.matches(':hover, :focus-within')) {
                activate({ target: link, type: link.matches(':focus-within') ? 'focusin' : 'mouseover' });
            }
        },
        unregister(id) {
            // The component's DOM may already be removed when disposal reaches JS.
            const registration = registrations.get(id);
            clearTimeout(registration?.timer);
            registrations.delete(id);
            if (activeLink === registration?.link) {
                observer.disconnect();
                activeLink = undefined;
            }
        },
    };

    function schedulePosition() {
        if (!frame) {
            frame = requestAnimationFrame(position);
        }
    }

    function position() {
        frame = undefined;
        if (!activeLink?.isConnected) {
            observer.disconnect();
            activeLink = undefined;
            return;
        }

        const preview = activeLink.querySelector('.insertion-preview');
        if (!preview.getClientRects().length) {
            return;
        }

        const anchor = activeLink.querySelector(':scope > a').getBoundingClientRect();
        const margin = 8;
        const viewportWidth = document.documentElement.clientWidth;
        const viewportHeight = document.documentElement.clientHeight;
        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize);
        const preferredWidth = Math.min(36 * rem, viewportWidth - 2 * rem);
        const preferredHeight = Math.min(28 * rem, viewportHeight * 0.6);
        const right = Math.max(0, viewportWidth - anchor.right - margin);
        const left = Math.max(0, anchor.left - margin);
        const minimumSideWidth = Math.min(preferredWidth, 20 * rem);
        const side = right >= preferredWidth ? 'right'
            : left >= preferredWidth ? 'left'
            : Math.max(left, right) >= minimumSideWidth ? (right >= left ? 'right' : 'left')
            : null;

        // Keep the build column unobstructed, narrowing the preview if needed.
        preview.style.maxWidth = `${side === 'right' ? right : side === 'left' ? left : preferredWidth}px`;
        if (side) {
            preview.style.maxHeight = `${preferredHeight}px`;
            const bounds = preview.getBoundingClientRect();
            preview.style.left = `${side === 'right' ? anchor.right : anchor.left - bounds.width}px`;
            preview.style.top = `${Math.max(margin, Math.min(anchor.top, viewportHeight - bounds.height - margin))}px`;
        } else {
            const below = Math.max(0, viewportHeight - anchor.bottom - margin);
            const above = Math.max(0, anchor.top - margin);
            const useAbove = below < Math.min(preview.scrollHeight, preferredHeight) && above > below;
            preview.style.maxHeight = `${Math.min(preferredHeight, useAbove ? above : below)}px`;
            const bounds = preview.getBoundingClientRect();
            preview.style.left = `${Math.max(margin, Math.min(anchor.right - bounds.width, viewportWidth - bounds.width - margin))}px`;
            preview.style.top = `${useAbove ? anchor.top - bounds.height : anchor.bottom}px`;
        }
        preview.style.visibility = 'visible';
    }

    function activate(event) {
        const link = event.target instanceof Element && event.target.closest('.insertion-build-link');
        if (!link) {
            return;
        }

        if (!(event.relatedTarget instanceof Node) || !link.contains(event.relatedTarget)) {
            link.querySelector('.insertion-preview').classList.remove('dismissed');
        }
        if (activeLink !== link) {
            const previousLink = activeLink;
            observer.disconnect();
            activeLink?.classList.remove('preview-active');
            activeLink = link;
            link.classList.add('preview-active');
            observer.observe(link.querySelector('.insertion-preview'));
            if (previousLink) {
                updateLoading(previousLink);
            }
        }
        updateLoading(link, event.type === 'focusin');
        schedulePosition();
    }

    function deactivate(event) {
        const link = event.target instanceof Element && event.target.closest('.insertion-build-link');
        if (link && (!(event.relatedTarget instanceof Node) || !link.contains(event.relatedTarget))) {
            // Wait for focus to settle; tabbing within the preview must not cancel.
            queueMicrotask(() => updateLoading(link));
        }
    }

    document.addEventListener('mouseover', activate);
    document.addEventListener('focusin', activate);
    document.addEventListener('mouseout', deactivate);
    document.addEventListener('focusout', deactivate);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            const preview = activeLink?.querySelector('.insertion-preview');
            if (preview?.contains(document.activeElement)) {
                activeLink.querySelector(':scope > a').focus({ preventScroll: true });
            }
            preview?.classList.add('dismissed');
            if (activeLink) {
                updateLoading(activeLink);
            }
        }
    });
    document.addEventListener('scroll', schedulePosition, true);
    window.addEventListener('resize', schedulePosition);
})();
