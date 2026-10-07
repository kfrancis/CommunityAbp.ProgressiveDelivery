/**
 * Select2-based pickers that find subjects (users by user name / name / email, tenants by name, and any subject
 * that already has assignments) through the subject lookup API, so nobody has to paste ids. Any typed value can
 * still be used as the id for subject types without a lookup provider.
 */
(function ($) {
    var pd = window.progressiveDelivery = window.progressiveDelivery || {};
    var l = abp.localization.getResource('ProgressiveDelivery');

    function lookupService() {
        return communityAbp.progressiveDelivery.subjects.featureSubjectLookup;
    }

    function toOption(subject) {
        return { id: subject.subjectId, text: subject.displayName, subject: subject };
    }

    function renderResult(item) {
        if (item.loading) {
            return item.text;
        }

        if (item.newTag) {
            return $('<span/>').text(l('UseTypedId', item.id));
        }

        var subject = item.subject;
        if (!subject) {
            return item.text;
        }

        var $el = $('<div class="pd-subject-option"/>');
        var $title = $('<div/>').append($('<span class="fw-semibold"/>').text(subject.displayName)).appendTo($el);
        if (subject.hasAssignments) {
            $title.append(' ').append($('<span class="badge bg-info-subtle text-info"/>').text(l('HasAssignments')));
        }

        if (subject.detail) {
            $('<div class="small text-muted"/>').text(subject.detail).appendTo($el);
        }

        if (subject.displayName !== subject.subjectId) {
            $('<div class="small text-muted pd-mono"/>').text(subject.subjectId).appendTo($el);
        }

        return $el;
    }

    function renderSelection(item) {
        if (!item.id) {
            return item.text; // placeholder
        }

        var text = item.subject ? item.subject.displayName : item.text;
        return text && text !== item.id ? text + ' (' + item.id + ')' : (text || item.id);
    }

    /**
     * @param {jQuery} $select a <select> whose value is the subject id.
     * @param {{ getSubjectType: function(): string, getTenantId?: function(): string, dropdownParent?: jQuery,
     *           allowFreeText?: boolean, allowClear?: boolean, placeholder?: string }} options
     */
    pd.subjectPicker = function ($select, options) {
        var allowFreeText = options.allowFreeText !== false;

        $select.select2({
            width: '100%',
            dropdownParent: options.dropdownParent,
            placeholder: options.placeholder || l('SubjectSearchPlaceholder'),
            allowClear: !!options.allowClear,
            ajax: {
                delay: 250,
                transport: function (params, success, failure) {
                    return lookupService().search({
                        subjectType: options.getSubjectType(),
                        filter: params.data.term || null,
                        tenantId: (options.getTenantId && options.getTenantId()) || null,
                        maxResultCount: 20
                    }).then(success, failure);
                },
                processResults: function (data, params) {
                    var results = $.map(data.items, toOption);
                    var term = $.trim(params.term || '');

                    // Any typed value can be the id (client ids, custom types). Offered last: a directory match is
                    // the more likely intent and should be the highlighted default.
                    if (allowFreeText && term && !results.some(function (r) { return r.id.toLowerCase() === term.toLowerCase(); })) {
                        results.push({ id: term, text: term, newTag: true });
                    }

                    return { results: results };
                }
            },
            templateResult: renderResult,
            templateSelection: renderSelection
        });

        return $select;
    };

    /** Host-only tenant picker; empty means the host. */
    pd.tenantPicker = function ($select, options) {
        return pd.subjectPicker($select, $.extend({}, options, {
            getSubjectType: function () { return 'Tenant'; },
            getTenantId: null,
            allowFreeText: false,
            allowClear: true,
            placeholder: l('TenantPlaceholder')
        }));
    };
})(jQuery);
