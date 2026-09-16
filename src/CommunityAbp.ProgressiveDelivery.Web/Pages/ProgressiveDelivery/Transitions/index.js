$(function () {
    var l = abp.localization.getResource('ProgressiveDelivery');
    var service = communityAbp.progressiveDelivery.transitions.featureTransition;
    var typeKeys = ['Promotion', 'AutomaticPromotion', 'ManualOverride', 'AutomaticDemotion', 'OfficialLevelChanged', 'AdministrativeReset', 'EmergencyOverride'];

    function esc(value) {
        return $('<div/>').text(value == null ? '' : value).html();
    }

    var dataTable = $('#TransitionsTable').DataTable(abp.libs.datatables.normalizeConfiguration({
        serverSide: true,
        paging: true,
        searching: false,
        scrollX: true,
        order: [],
        ajax: abp.libs.datatables.createAjax(service.getList, function () {
            var type = $('#TypeFilter').val();
            return {
                featureTrackId: $('#TrackFilter').val() || null,
                transitionType: type === '' || type == null ? null : parseInt(type, 10),
                subjectType: $('#SubjectTypeFilter').val() || null,
                subjectId: $('#SubjectIdFilter').val() || null
            };
        }),
        columnDefs: [
            {
                title: l('CreationTime'), data: 'creationTime', render: function (d) {
                    return d ? luxon.DateTime.fromISO(d, { locale: abp.localization.currentCulture.name }).toRelative() : '';
                }
            },
            { title: l('Track'), data: 'trackName', render: function (d) { return '<span class="pd-mono">' + esc(d) + '</span>'; } },
            {
                title: l('TransitionType'), data: 'transitionType', render: function (d) {
                    return '<span class="pd-transition pd-transition-' + (typeKeys[d] || d) + '">' + esc(l('Enum:FeatureTransitionType.' + d)) + '</span>';
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

    $('#TrackFilter, #TypeFilter').on('change', function () { dataTable.ajax.reload(); });
    $('#SubjectTypeFilter, #SubjectIdFilter').on('keyup', function () { dataTable.ajax.reload(); });

    $(document).on('click', '.pd-chip', function () {
        var text = $(this).text();
        if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(function () { abp.notify.info(l('CopiedToClipboard')); });
        }
    });
});
