$(function () {
    var l = abp.localization.getResource('ProgressiveDelivery');
    var assignmentService = communityAbp.progressiveDelivery.assignments.featureAssignment;
    var overrideModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'ProgressiveDelivery/Assignments/OverrideModal',
        scriptUrl: abp.appPath + 'Pages/ProgressiveDelivery/Assignments/override-modal.js',
        modalClass: 'ProgressiveDeliveryOverride'
    });

    var $form = $('#InspectForm');
    var $subjectType = $form.find('[name="SubjectType"]');
    var $subject = $form.find('.pd-subject-picker');
    var $tenant = $form.find('.pd-tenant-picker');

    function isTenantSubject() {
        return ($subjectType.val() || '').toLowerCase() === 'tenant';
    }

    function syncTenantField() {
        $form.find('.pd-tenant-field').toggle(!isTenantSubject());
    }

    progressiveDelivery.subjectPicker($subject, {
        getSubjectType: function () { return $subjectType.val(); },
        getTenantId: function () { return isTenantSubject() ? null : $tenant.val(); }
    });

    if ($tenant.length) {
        progressiveDelivery.tenantPicker($tenant, {});
    }

    $subjectType.on('change', function () {
        $subject.val(null).trigger('change');
        syncTenantField();
    });

    // Picking a subject is the search: no extra click needed.
    $subject.on('select2:select', function () {
        if (isTenantSubject()) {
            $tenant.val(null);
        }
        $form.trigger('submit');
    });

    syncTenantField();

    overrideModal.onResult(function () {
        window.location.reload();
    });

    $('.pd-inspect-override').click(function () {
        var $card = $(this).closest('.pd-inspect-card');
        overrideModal.open({
            trackName: $card.data('track-name'),
            subjectType: $card.data('subject-type'),
            subjectId: $card.data('subject-id'),
            tenantId: $card.data('tenant-id') || null
        });
    });

    $('.pd-inspect-reset').click(function () {
        var $card = $(this).closest('.pd-inspect-card');
        var subject = $card.data('subject-type') + ' ' + $card.data('subject-name');
        abp.message.confirm(l('ResetConfirmationMessage', subject)).then(function (confirmed) {
            if (!confirmed) { return; }
            assignmentService.reset({
                trackName: $card.data('track-name'),
                subjectType: $card.data('subject-type'),
                subjectId: $card.data('subject-id'),
                tenantId: $card.data('tenant-id') || null,
                useCurrentTenant: !$card.data('tenant-id'),
                reason: 'Reset from subject inspection'
            }).then(function () {
                abp.notify.info(l('SavedSuccessfully'));
                window.location.reload();
            });
        });
    });
});
