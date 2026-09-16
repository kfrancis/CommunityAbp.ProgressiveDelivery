$(function () {
    var l = abp.localization.getResource('ProgressiveDelivery');
    var $root = $('#TrackDetail');
    var trackId = $root.data('track-id');
    var trackName = $root.data('track-name');

    var trackService = communityAbp.progressiveDelivery.tracks.featureTrack;
    var rolloutService = communityAbp.progressiveDelivery.rollouts.featureRollout;
    var assignmentService = communityAbp.progressiveDelivery.assignments.featureAssignment;
    var transitionService = communityAbp.progressiveDelivery.transitions.featureTransition;

    var canOverride = abp.auth.isGranted('ProgressiveDelivery.Assignments.Override');

    var editTrackModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/EditModal');
    var promoteModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/SetOfficialLevelModal');
    var addLevelModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/AddLevelModal');
    var editLevelModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/EditLevelModal');
    var startRolloutModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Rollouts/StartModal');
    var editRolloutModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Rollouts/EditModal');
    var overrideModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Assignments/OverrideModal');

    function reload() {
        window.location.reload();
    }

    function esc(value) {
        return $('<div/>').text(value == null ? '' : value).html();
    }

    function relative(value) {
        return value ? luxon.DateTime.fromISO(value, { locale: abp.localization.currentCulture.name }).toRelative() : '';
    }

    [editTrackModal, promoteModal, addLevelModal, editLevelModal, startRolloutModal, editRolloutModal].forEach(function (m) {
        m.onResult(reload);
    });

    $('#EditTrackButton').click(function (e) { e.preventDefault(); editTrackModal.open({ id: trackId }); });
    $('#PromoteButton').click(function (e) { e.preventDefault(); promoteModal.open({ trackId: trackId }); });
    $('#AddLevelButton').click(function (e) { e.preventDefault(); addLevelModal.open({ trackId: trackId }); });
    $('#NewRolloutButton').click(function (e) { e.preventDefault(); startRolloutModal.open({ trackId: trackId }); });

    $root.on('click', '.pd-edit-level', function () {
        editLevelModal.open({ trackId: trackId, level: $(this).data('level') });
    });

    $root.on('click', '.pd-promote-level', function () {
        promoteModal.open({ trackId: trackId, level: $(this).data('level') });
    });

    $root.on('click', '.pd-rollout-edit', function () {
        editRolloutModal.open({ trackId: trackId, targetLevel: $(this).data('level') });
    });

    $root.on('click', '.pd-rollout-status', function () {
        var level = $(this).data('level');
        rolloutService.update(trackId, level, { status: $(this).data('status') }).then(function () {
            abp.notify.info(l('SavedSuccessfully'));
            reload();
        });
    });

    $root.on('click', '.pd-rollout-suspend', function () {
        var level = $(this).data('level');
        abp.message.confirm(l('SuspendRolloutConfirmationMessage', level)).then(function (confirmed) {
            if (!confirmed) { return; }
            rolloutService.update(trackId, level, { status: 3 }).then(function () {
                abp.notify.info(l('SavedSuccessfully'));
                reload();
            });
        });
    });

    $root.on('click', '.pd-rollout-remove', function () {
        var level = $(this).data('level');
        abp.message.confirm(l('RemoveRolloutConfirmationMessage', level)).then(function (confirmed) {
            if (!confirmed) { return; }
            rolloutService.delete(trackId, level).then(function () {
                abp.notify.info(l('SuccessfullyDeleted'));
                reload();
            });
        });
    });

    $('#ToggleEnabledButton').click(function () {
        var isEnabled = $root.data('is-enabled') === true || $root.data('is-enabled') === 'true';
        var message = isEnabled ? l('DisableTrackConfirmationMessage', trackName) : l('EnableTrackConfirmationMessage', trackName);
        abp.message.confirm(message).then(function (confirmed) {
            if (!confirmed) { return; }
            trackService.update(trackId, {
                displayName: $root.data('display-name') || null,
                description: $root.data('description') || null,
                isEnabled: !isEnabled,
                concurrencyStamp: $root.data('concurrency-stamp')
            }).then(function () {
                abp.notify.info(l('SavedSuccessfully'));
                reload();
            });
        });
    });

    $('#DeleteTrackButton').click(function () {
        abp.message.confirm(l('TrackDeletionConfirmationMessage', trackName)).then(function (confirmed) {
            if (!confirmed) { return; }
            trackService.delete(trackId).then(function () {
                abp.notify.info(l('SuccessfullyDeleted'));
                window.location.href = abp.appPath + 'ProgressiveDelivery/Tracks';
            });
        });
    });

    if ($('#AssignmentsTable').length) {
        var assignmentsTable = $('#AssignmentsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            paging: true,
            searching: false,
            scrollX: true,
            order: [],
            ajax: abp.libs.datatables.createAjax(assignmentService.getList, function () {
                return { featureTrackId: trackId };
            }),
            columnDefs: [
                {
                    title: l('Actions'),
                    rowAction: {
                        items: [
                            {
                                text: l('Inspect'),
                                action: function (data) {
                                    window.location.href = abp.appPath + 'ProgressiveDelivery/Inspection?subjectType=' + encodeURIComponent(data.record.subjectType)
                                        + '&subjectId=' + encodeURIComponent(data.record.subjectId) + '&trackName=' + encodeURIComponent(trackName);
                                }
                            },
                            {
                                text: l('Override'),
                                visible: canOverride,
                                action: function (data) {
                                    overrideModal.open({ trackName: trackName, subjectType: data.record.subjectType, subjectId: data.record.subjectId, level: data.record.assignedLevel });
                                }
                            },
                            {
                                text: l('ResetToOfficial'),
                                visible: canOverride,
                                confirmMessage: function (data) {
                                    return l('ResetConfirmationMessage', data.record.subjectType + ' ' + data.record.subjectId);
                                },
                                action: function (data) {
                                    assignmentService.reset({ trackName: trackName, subjectType: data.record.subjectType, subjectId: data.record.subjectId, reason: 'Reset from track detail' }).then(function () {
                                        abp.notify.info(l('SavedSuccessfully'));
                                        assignmentsTable.ajax.reload();
                                    });
                                }
                            }
                        ]
                    }
                },
                { title: l('SubjectType'), data: 'subjectType' },
                { title: l('SubjectId'), data: 'subjectId', render: function (d) { return '<span class="pd-mono">' + esc(d) + '</span>'; } },
                {
                    title: l('AssignedLevel'), data: 'assignedLevel', render: function (d) {
                        var official = parseInt($root.data('official-level'), 10);
                        var tone = d > official ? 'experimental' : (d === official ? 'official' : 'muted');
                        var note = d < official ? ' <span class="text-muted small">' + l('ResolvesToOfficial', official) + '</span>' : '';
                        return '<span class="pd-pill pd-pill-' + tone + '">' + d + '</span>' + note;
                    }
                },
                { title: l('Reason'), data: 'assignmentReason', orderable: false, render: esc },
                { title: l('TenantId'), data: 'tenantId', render: function (d) { return d ? '<span class="pd-mono small">' + esc(d) + '</span>' : '<span class="text-muted">' + l('Host') + '</span>'; } },
                { title: l('LastModificationTime'), data: 'lastModificationTime', render: function (d, t, row) { return relative(d || row.creationTime); } }
            ]
        }));

        overrideModal.onResult(function () {
            assignmentsTable.ajax.reload();
        });

        $('#OverrideButton').click(function (e) {
            e.preventDefault();
            overrideModal.open({ trackName: trackName });
        });
    }

    if ($('#HistoryTable').length) {
        $('#HistoryTable').DataTable(abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            paging: true,
            searching: false,
            scrollX: true,
            order: [],
            ajax: abp.libs.datatables.createAjax(transitionService.getList, function () {
                return { featureTrackId: trackId };
            }),
            columnDefs: [
                { title: l('CreationTime'), data: 'creationTime', render: relative },
                {
                    title: l('TransitionType'), data: 'transitionType', render: function (d) {
                        var name = l('Enum:FeatureTransitionType.' + d);
                        var key = ['Promotion', 'AutomaticPromotion', 'ManualOverride', 'AutomaticDemotion', 'OfficialLevelChanged', 'AdministrativeReset', 'EmergencyOverride'][d] || d;
                        return '<span class="pd-transition pd-transition-' + key + '">' + esc(name) + '</span>';
                    }
                },
                {
                    title: l('Change'), data: 'toLevel', orderable: false, render: function (d, t, row) {
                        return '<span class="pd-mono">L' + (row.fromLevel == null ? '&mdash;' : row.fromLevel) + ' &rarr; L' + d + '</span>';
                    }
                },
                {
                    title: l('Subject'), data: 'subjectId', orderable: false, render: function (d, t, row) {
                        return row.subjectType ? esc(row.subjectType) + ' <span class="pd-mono small">' + esc(d) + '</span>' : '<span class="text-muted">' + l('Track') + '</span>';
                    }
                },
                { title: l('Reason'), data: 'reason', orderable: false, render: esc },
                {
                    title: l('Correlation'), data: 'correlationId', orderable: false, render: function (d, t, row) {
                        var chips = '';
                        if (d) { chips += '<span class="pd-chip" title="' + l('CorrelationId') + '">' + esc(d) + '</span> '; }
                        if (row.traceId) { chips += '<span class="pd-chip" title="' + l('TraceId') + '">' + esc(row.traceId) + '</span>'; }
                        return chips;
                    }
                }
            ]
        }));
    }
});
