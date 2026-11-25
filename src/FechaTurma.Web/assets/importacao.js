/* Progressive enhancement: the preview remains usable without JavaScript. */
jQuery(function ($) {
  const $filter = $('[data-pending-filter]');
  const $rows = $('tr[data-pending]');
  if (!$filter.length || !$rows.length || !$rows.filter('[data-pending="true"]').length) return;
  const update = function () {
    const onlyPending = $filter.prop('checked');
    $rows.each(function () { this.hidden = onlyPending && this.dataset.pending !== 'true'; });
    const count = $rows.filter(function () { return !this.hidden; }).length;
    const label = $rows.length === 1 ? 'matrícula exibida' : 'matrículas exibidas';
    $('[data-visible-count]').text(`${count} de ${$rows.length} ${label}.`);
  };
  $('[data-filter-panel]').prop('hidden', false);
  $filter.on('change', update);
  update();
});
