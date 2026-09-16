$(function () {
    var l = abp.localization.getResource('ProgressiveDelivery');
    var assignmentService = communityAbp.progressiveDelivery.assignments.featureAssignment;
    var overrideModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Assignments/OverrideModal');

    overrideModal.onResult(function () {
        window.location.reload();
    });

    $('.pd-inspect-override').click(function () {
        var $card = $(this).closest('.pd-inspect-card');
        overrideModal.open({
            trackName: $card.data('track-name'),
            subjectType: $card.data('subject-type'),
            subjectId: $card.data('subject-id')
        });
    });

    $('.pd-inspect-reset').click(function () {
        var $card = $(this).closest('.pd-inspect-card');
        var subject = $card.data('subject-type') + ' ' + $card.data('subject-id');
        abp.message.confirm(l('ResetConfirmationMessage', subject)).then(function (confirmed) {
            if (!confirmed) { return; }
            assignmentService.reset({
                trackName: $card.data('track-name'),
                subjectType: $card.data('subject-type'),
                subjectId: $card.data('subject-id'),
                reason: 'Reset from subject inspection'
            }).then(function () {
                abp.notify.info(l('SavedSuccessfully'));
                window.location.reload();
            });
        });
    });
});
