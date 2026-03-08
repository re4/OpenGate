window.captchaInterop = {
    render: function (containerId, provider, siteKey, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';

        if (provider === 'RecaptchaV2') {
            if (typeof grecaptcha === 'undefined') return;
            grecaptcha.ready(function () {
                grecaptcha.render(container, {
                    sitekey: siteKey,
                    callback: function (token) {
                        dotNetRef.invokeMethodAsync('OnCaptchaCallback', token);
                    },
                    'expired-callback': function () {
                        dotNetRef.invokeMethodAsync('OnCaptchaCallback', '');
                    },
                    theme: 'dark'
                });
            });
        } else if (provider === 'RecaptchaV3') {
            if (typeof grecaptcha === 'undefined') return;
            grecaptcha.ready(function () {
                grecaptcha.execute(siteKey, { action: 'submit' }).then(function (token) {
                    dotNetRef.invokeMethodAsync('OnCaptchaCallback', token);
                });
            });
        } else if (provider === 'HCaptcha') {
            if (typeof hcaptcha === 'undefined') return;
            hcaptcha.render(container, {
                sitekey: siteKey,
                callback: function (token) {
                    dotNetRef.invokeMethodAsync('OnCaptchaCallback', token);
                },
                'expired-callback': function () {
                    dotNetRef.invokeMethodAsync('OnCaptchaCallback', '');
                },
                theme: 'dark'
            });
        } else if (provider === 'Turnstile') {
            if (typeof turnstile === 'undefined') return;
            turnstile.render(container, {
                sitekey: siteKey,
                callback: function (token) {
                    dotNetRef.invokeMethodAsync('OnCaptchaCallback', token);
                },
                'expired-callback': function () {
                    dotNetRef.invokeMethodAsync('OnCaptchaCallback', '');
                },
                theme: 'dark'
            });
        }
    },

    executeV3: function (siteKey) {
        return new Promise(function (resolve) {
            if (typeof grecaptcha === 'undefined') { resolve(''); return; }
            grecaptcha.ready(function () {
                grecaptcha.execute(siteKey, { action: 'submit' }).then(resolve);
            });
        });
    },

    setToken: function (elementId, token) {
        var el = document.getElementById(elementId);
        if (el) el.value = token;
    },

    getFormToken: function (provider) {
        if (provider === 'RecaptchaV2' || provider === 'RecaptchaV3') {
            var el = document.querySelector('[name="g-recaptcha-response"]');
            return el ? el.value : '';
        } else if (provider === 'HCaptcha') {
            var el = document.querySelector('[name="h-captcha-response"]');
            return el ? el.value : '';
        } else if (provider === 'Turnstile') {
            var el = document.querySelector('[name="cf-turnstile-response"]');
            return el ? el.value : '';
        }
        return '';
    }
};
