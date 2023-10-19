window.initializeDataTable = (selector, responsive, lengthChange, autoWidth, buttons) => {
    $(selector).DataTable({
        responsive: responsive,
        lengthChange: lengthChange,
        autoWidth: autoWidth,
        buttons: buttons
    }).buttons().container().appendTo(`${selector}_wrapper .col-md-6:eq(0)`);
};