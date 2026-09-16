$(function () {
    var l = abp.localization.getResource('ProgressiveDelivery');
    var service = communityAbp.progressiveDelivery.tracks.featureTrack;
    var canManage = abp.auth.isGranted('ProgressiveDelivery.Tracks.Manage');

    var createModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/CreateModal');
    var editModal = new abp.ModalManager(abp.appPath + 'ProgressiveDelivery/Tracks/EditModal');

    function levelPill(level, tone) {
        return '<span class="pd-pill pd-pill-' + tone + '">' + level + '</span>';
    }

    function rolloutSummary(rollouts) {
        if (!rollouts || !rollouts.length) {
            return '<span class="text-muted">&mdash;</span>';
        }
        return rollouts.map(function (r) {
            var pct = (r.percentageBasisPoints / 100).toFixed(2) + ' %';
            var state = r.status === 0 ? '' : ' <span class="text-muted">(' + l('Enum:FeatureRolloutStatus.' + r.status) + ')</span>';
            return '<span class="pd-mono">L' + r.targetLevel + ' &rarr; ' + pct + '</span>' + state;
        }).join('<br/>');
    }

    var dataTable = $('#TracksTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        order: [[1, 'asc']],
        searching: false,
        scrollX: true,
        ajax: abp.libs.datatables.createAjax(service.getList, function () {
            return { filter: $('#TrackFilter').val() };
        }),
        columnDefs: [
            {
                title: l('Actions'),
                rowAction: {
                    items: [
                        {
                            text: l('Open'),
                            action: function (data) {
                                window.location.href = abp.appPath + 'ProgressiveDelivery/Tracks/Detail?id=' + data.record.id;
                            }
                        },
                        {
                            text: l('Edit'),
                            visible: canManage,
                            action: function (data) {
                                editModal.open({ id: data.record.id });
                            }
                        },
                        {
                            text: l('Delete'),
                            visible: canManage,
                            confirmMessage: function (data) {
                                return l('TrackDeletionConfirmationMessage', data.record.name);
                            },
                            action: function (data) {
                                service.delete(data.record.id).then(function () {
                                    abp.notify.info(l('SuccessfullyDeleted'));
                                    dataTable.ajax.reload();
                                });
                            }
                        }
                    ]
                }
            },
            {
                title: l('Track'),
                data: 'name',
                render: function (data, type, row) {
                    var display = row.displayName ? '<div class="text-muted small">' + $('<div/>').text(row.displayName).html() + '</div>' : '';
                    return '<a class="pd-mono fw-semibold" href="' + abp.appPath + 'ProgressiveDelivery/Tracks/Detail?id=' + row.id + '">' + $('<div/>').text(data).html() + '</a>' + display;
                }
            },
            {
                title: l('OfficialHighest'),
                data: 'officialLevel',
                render: function (data, type, row) {
                    return levelPill(data, 'official') + ' <span class="text-muted">/</span> ' + levelPill(row.highestAvailableLevel, row.highestAvailableLevel > data ? 'experimental' : 'muted');
                }
            },
            {
                title: l('Status'),
                data: 'isEnabled',
                render: function (data) {
                    return data
                        ? '<span class="badge bg-success-subtle text-success">' + l('Enabled') + '</span>'
                        : '<span class="badge bg-secondary-subtle text-secondary">' + l('Disabled') + '</span>';
                }
            },
            {
                title: l('ActiveRollouts'),
                data: 'rollouts',
                orderable: false,
                render: rolloutSummary
            },
            {
                title: l('LastModificationTime'),
                data: 'lastModificationTime',
                render: function (data, type, row) {
                    var value = data || row.creationTime;
                    return value ? luxon.DateTime.fromISO(value, { locale: abp.localization.currentCulture.name }).toRelative() : '';
                }
            }
        ]
    }));

    $('#TrackFilter').on('keyup', abp.utils.debounce ? abp.utils.debounce(function () { dataTable.ajax.reload(); }, 300) : function () { dataTable.ajax.reload(); });

    $('#NewTrackButton').click(function (e) {
        e.preventDefault();
        createModal.open();
    });

    createModal.onResult(function () {
        dataTable.ajax.reload();
    });

    editModal.onResult(function () {
        dataTable.ajax.reload();
    });
});
