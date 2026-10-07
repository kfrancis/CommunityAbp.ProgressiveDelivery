abp.modals.ProgressiveDeliveryOverride = function () {
    this.initModal = function (modalManager) {
        var $modal = modalManager.getModal();
        var $form = modalManager.getForm();
        var $subjectType = $form.find('[name="Input.SubjectType"]');
        var $subject = $form.find('.pd-subject-picker');
        var $tenant = $form.find('.pd-tenant-picker');
        var $tenantField = $form.find('.pd-tenant-field');

        function isTenantSubject() {
            return ($subjectType.val() || '').toLowerCase() === 'tenant';
        }

        function syncTenantField() {
            // A tenant subject's tenant is the tenant itself; the picker would only confuse.
            $tenantField.toggle(!isTenantSubject());
        }

        progressiveDelivery.subjectPicker($subject, {
            dropdownParent: $modal,
            getSubjectType: function () { return $subjectType.val(); },
            getTenantId: function () { return isTenantSubject() ? null : $tenant.val(); }
        });

        if ($tenant.length) {
            progressiveDelivery.tenantPicker($tenant, { dropdownParent: $modal });
            $tenant.on('change', function () {
                $subject.val(null).trigger('change');
            });
        }

        $subjectType.on('change', function () {
            $subject.val(null).trigger('change');
            syncTenantField();
        });

        syncTenantField();
    };
};
