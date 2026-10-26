// Українські тексти стандартних правил jQuery Validation.
// Потрібні для правил, які плагін бере з HTML-атрибутів (type="number", min, max, step, required).
(function ($) {
    $.extend($.validator.messages, {
        required: "Заповніть це поле.",
        remote: "Виправте це поле.",
        email: "Введіть коректний e-mail.",
        url: "Введіть коректну адресу URL.",
        date: "Введіть коректну дату.",
        dateISO: "Введіть коректну дату у форматі ISO.",
        number: "Введіть число.",
        digits: "Вводьте лише цифри.",
        equalTo: "Значення не збігаються.",
        maxlength: $.validator.format("Введіть не більше {0} символів."),
        minlength: $.validator.format("Введіть щонайменше {0} символів."),
        rangelength: $.validator.format("Введіть від {0} до {1} символів."),
        range: $.validator.format("Введіть число від {0} до {1}."),
        max: $.validator.format("Введіть число, не більше за {0}."),
        min: $.validator.format("Введіть число, не менше за {0}."),
        step: $.validator.format("Введіть число, кратне {0}.")
    });
})(jQuery);
