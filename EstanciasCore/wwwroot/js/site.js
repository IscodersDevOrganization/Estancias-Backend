// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// ========================================================================
// 1. GLOBAL DATATABLES DEFAULTS & STATE RETENTION
// ========================================================================
if (typeof $ !== 'undefined' && $.fn && $.fn.dataTable) {
    $.extend(true, $.fn.dataTable.defaults, {
        stateSave: true,
        stateDuration: -1, // -1 = sessionStorage
        stateSaveCallback: function (settings, data) {
            var norm = (window.location.pathname || '').toLowerCase().replace(/\/index\/?$/, '').replace(/\/+$/, '') || '/';
            var key = 'DataTables_' + settings.sInstance + '_' + norm;
            try {
                var json = JSON.stringify(data);
                sessionStorage.setItem(key, json);
                localStorage.setItem(key, json);
            } catch (e) {}
        },
        stateLoadCallback: function (settings) {
            var norm = (window.location.pathname || '').toLowerCase().replace(/\/index\/?$/, '').replace(/\/+$/, '') || '/';
            var key = 'DataTables_' + settings.sInstance + '_' + norm;
            try {
                var d = sessionStorage.getItem(key);
                if (!d) d = localStorage.getItem(key);
                return d ? JSON.parse(d) : null;
            } catch (e) {
                return null;
            }
        }
    });
}

// ========================================================================
// 2. UNIVERSAL FILTER STATE MANAGER (PORTAL-WIDE)
// ========================================================================
window.PortalFilterManager = (function () {
    var PREFIX = 'portal_flt_';

    function normalizePath(p) {
        var path = (p || window.location.pathname || '').toLowerCase();
        path = path.replace(/\/index\/?$/, '');
        path = path.replace(/\/+$/, '');
        return path || '/';
    }

    function getStorageKey(el) {
        var id = el.id || $(el).attr('name');
        if (!id) return null;
        var path = normalizePath();
        return PREFIX + path + '_' + id.toLowerCase();
    }

    function isFilterElement(el) {
        var $el = $(el);
        if ($el.closest('.modal').length) return false;
        if ($el.is(':file') || $el.is(':password') || $el.is('button')) return false;
        if ($el.attr('name') === '__RequestVerificationToken') return false;

        var id = (el.id || '').toLowerCase();
        var name = ($el.attr('name') || '').toLowerCase();

        if (id.indexOf('filtro') > -1 || id.indexOf('filter') > -1 ||
            name.indexOf('filtro') > -1 || name.indexOf('filter') > -1) {
            return true;
        }

        if (id.indexOf('marca') > -1 || id.indexOf('customsearch') > -1 || id.indexOf('custompage') > -1) {
            return true;
        }

        if ($el.closest('.filtro-grilla, .card-toolbar, .card-toolbar-right, .box-header, #filtrosContainer, .dataTables_filter').length) {
            if ($el.is('select, input:text, input[type="search"], input[type="date"]')) {
                return true;
            }
        }

        return false;
    }

    function save(el) {
        if (!isFilterElement(el)) return;
        var key = getStorageKey(el);
        if (!key) return;

        var val = $(el).val();
        if (val === null || val === undefined || val === '') {
            sessionStorage.removeItem(key);
            localStorage.removeItem(key);
        } else {
            var strVal = JSON.stringify(val);
            sessionStorage.setItem(key, strVal);
            localStorage.setItem(key, strVal);
        }
    }

    function get(id) {
        var path = normalizePath();
        var key = PREFIX + path + '_' + id.toLowerCase();
        var stored = sessionStorage.getItem(key);
        if (stored === null || stored === undefined) {
            stored = localStorage.getItem(key);
        }
        if (stored !== null && stored !== undefined) {
            try { return JSON.parse(stored); } catch (e) { return stored; }
        }
        return null;
    }

    function set(id, val) {
        var path = normalizePath();
        var key = PREFIX + path + '_' + id.toLowerCase();
        if (val === null || val === undefined || val === '') {
            sessionStorage.removeItem(key);
            localStorage.removeItem(key);
        } else {
            var strVal = JSON.stringify(val);
            sessionStorage.setItem(key, strVal);
            localStorage.setItem(key, strVal);
        }
    }

    function restore() {
        $('select, input').each(function () {
            if (!isFilterElement(this)) return;
            var key = getStorageKey(this);
            if (!key) return;

            var stored = sessionStorage.getItem(key);
            if (stored === null || stored === undefined) {
                stored = localStorage.getItem(key);
            }
            if (stored !== null && stored !== undefined) {
                try {
                    var val = JSON.parse(stored);
                    var currentVal = $(this).val();
                    if (val !== currentVal) {
                        $(this).val(val);
                        if ($(this).data('selectpicker')) {
                            $(this).selectpicker('val', val);
                        } else if ($(this).hasClass('select2-hidden-accessible')) {
                            $(this).trigger('change.select2');
                        }
                        $(this).trigger('change');
                        $(this).trigger('input');
                    }
                } catch (e) {
                    console.warn('Error al restaurar filtro portal:', e);
                }
            }
        });
    }

    function clearCurrentPage() {
        var path = normalizePath();
        var prefix = PREFIX + path + '_';

        // 1. Limpiar almacenamiento de sesión y local para filtros y DataTables de la ruta actual
        var toRemoveSession = [];
        for (var i = 0; i < sessionStorage.length; i++) {
            var k = sessionStorage.key(i);
            if (k && (k.indexOf(prefix) === 0 || k.indexOf('DataTables_') === 0)) {
                toRemoveSession.push(k);
            }
        }
        for (var si = 0; si < toRemoveSession.length; si++) {
            sessionStorage.removeItem(toRemoveSession[si]);
        }

        var toRemoveLocal = [];
        for (var j = 0; j < localStorage.length; j++) {
            var lk = localStorage.key(j);
            if (lk && (lk.indexOf(prefix) === 0 || lk.indexOf('DataTables_') === 0)) {
                toRemoveLocal.push(lk);
            }
        }
        for (var li = 0; li < toRemoveLocal.length; li++) {
            localStorage.removeItem(toRemoveLocal[li]);
        }

        // 2. Resetear todos los inputs y selects de filtros en la página
        $('.filtro-grilla, .card-toolbar, .box-header, #filtrosContainer, .dataTables_wrapper, .has-custom-toolbar').find('input:text, input[type="search"], select').each(function () {
            var $el = $(this);
            if ($el.is('select')) {
                var hasEmpty = $el.find('option[value=""]').length > 0;
                if (hasEmpty) {
                    $el.val('');
                } else {
                    $el.prop('selectedIndex', 0);
                }
                if ($el.data('selectpicker')) {
                    $el.selectpicker('refresh');
                } else if ($el.hasClass('select2-hidden-accessible')) {
                    $el.trigger('change.select2');
                }
                $el.trigger('change');
            } else {
                $el.val('');
                $el.trigger('input');
                $el.trigger('change');
            }
        });

        // 3. Resetear DataTables en la página
        if (typeof $ !== 'undefined' && $.fn && $.fn.dataTable) {
            $('.dataTable, table[id]').each(function () {
                if ($.fn.dataTable.isDataTable(this)) {
                    try {
                        var dt = $(this).DataTable();
                        if (dt) {
                            dt.search('').columns().search('');
                            if (dt.state && dt.state.clear) {
                                dt.state.clear();
                            }
                            if (dt.ajax && typeof dt.ajax.reload === 'function') {
                                dt.ajax.reload();
                            } else {
                                dt.draw();
                            }
                        }
                    } catch (err) {}
                }
            });
        }
    }

    return {
        save: save,
        restore: restore,
        get: get,
        set: set,
        normalizePath: normalizePath,
        isFilterElement: isFilterElement,
        clearCurrentPage: clearCurrentPage
    };
})();

// Listen for any filter changes in the portal
$(document).on('change input', 'select, input', function () {
    if (window.PortalFilterManager) {
        window.PortalFilterManager.save(this);
    }
});

// Botón universal de Limpiar Filtros
$(document).on('click', '.btn-clear-filters, .btn-clear-filters-sm', function (e) {
    if (window.PortalFilterManager && window.PortalFilterManager.clearCurrentPage) {
        window.PortalFilterManager.clearCurrentPage();
    }
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            toast: true,
            position: 'top-end',
            icon: 'info',
            title: 'Filtros limpiados',
            showConfirmButton: false,
            timer: 2000
        });
    }
});

// Auto-adjuntar botón de Limpiar en tablas DataTables tradicionales si no lo tienen
function attachClearButtonsToDataTables() {
    if (typeof $ === 'undefined' || !$.fn || !$.fn.dataTable) return;

    $('.dataTables_filter').each(function () {
        var $filter = $(this);
        if ($filter.find('.btn-clear-filters-sm').length === 0 && !$filter.closest('.modern-card, .has-custom-toolbar').length) {
            var $clearBtn = $('<button type="button" class="btn-clear-filters-sm" title="Limpiar búsqueda y filtros">' +
                '<i class="fa fa-eraser"></i> Limpiar' +
                '</button>');
            $filter.append($clearBtn);
        }
    });
}

// Restore on ready, DataTable init, and Ajax load
$(document).ready(function () {
    if (window.PortalFilterManager) {
        window.PortalFilterManager.restore();
        setTimeout(window.PortalFilterManager.restore, 150);
        setTimeout(window.PortalFilterManager.restore, 400);
    }
    setTimeout(attachClearButtonsToDataTables, 250);
});

$(document).on('init.dt draw.dt', function () {
    if (window.PortalFilterManager) {
        setTimeout(window.PortalFilterManager.restore, 50);
    }
    attachClearButtonsToDataTables();
});

$(document).ajaxComplete(function (e, xhr, settings) {
    if (settings && settings.type === 'GET' && window.PortalFilterManager) {
        setTimeout(window.PortalFilterManager.restore, 100);
    }
});

// Write your JavaScript code.
$(document).ready(function () {
    /////********//// Funcion Captura cualquier select cuando cambia /////********////
    $('#CantHermanos').change(function () {
        var rel = getRelVF($('#CantHermanos').val(), $('#CantHermanosVivos').val())
        $('#CantHermanosFallecidos').val(rel);
    })
    $('#CantHermanosVivos').change(function () {
        var rel = getRelVF($('#CantHermanos').val(), $('#CantHermanosVivos').val())
        $('#CantHermanosFallecidos').val(rel);
    })
    $('#CantHijos').change(function () {
        var rel = getRelVF($('#CantHijos').val(), $('#CantHijosVivos').val())
        $('#CantHijosFallecidos').val(rel);
    })
    $('#CantHijosVivos').change(function () {
        var rel = getRelVF($('#CantHijos').val(), $('#CantHijosVivos').val())
        $('#CantHijosFallecidos').val(rel);
    })
    function getRelVF($total, $vivos) {
        var t = $total,
            v = $vivos,
            rel = t - v;
        return rel;
    };
    $("#Financiador_Id").change(function () {
        LoadPlanes();
    });

    function LoadPlanes() {
        var financiadorId = "";
        financiadorId += $("#Financiador_Id option:selected").val();
        $.ajax({
            url: "/FinanciadoresPlanes/JsonPlanes?=" + financiadorId,
            type: "POST",
            dataType: "JSON",
            success: function (planes) {
                if ($("#Financiador_Id").val() != null) {
                    $("#FinanciadoresPlanes_Id").prop('disabled', false)
                    $('button[data-id="FinanciadoresPlanes_Id"]').removeClass('disabled')
                } else {
                    $("#FinanciadoresPlanes_Id").prop('disabled', true)
                    $('button[data-id="FinanciadoresPlanes_Id"]').addClass('disabled')
                }
                $('button[data-id="FinanciadoresPlanes_Id"]').find('span.filter-option').html("Seleccione")
                $('#FinanciadoresPlanes_Id').val(0)
                $('#FinanciadoresPlanes_Id').empty()
                $('#planes').find('ul').empty()
                $('#planes').find('ul').append('<li data-original-index="0"><a tabindex="0" class="" data-normalized-text="<span class=&quot;text&quot;>Seleccione</span>"><span class="text">Seleccione</span><span class="glyphicon glyphicon-ok check-mark"></span></a></li>');
                $('#FinanciadoresPlanes_Id').append("<option value='0'>Seleccione</option>");
                $.each(planes, function (planes) {
                    $('#FinanciadoresPlanes_Id').append("<option value='" + this.Value + "'>" + this.Text + "</option>");
                    $('#planes').find('ul').append('<li data-original-index="' + this.Value + '"><a tabindex="0" class="" data-normalized-text="<span class=&quot;text&quot;>' + this.Text + '</span>"><span class="text">' + this.Text + '</span><span class="glyphicon glyphicon-ok check-mark"></span></a></li>');
                });
                $('#FinanciadoresPlanes_Id').selectpicker('refresh');
            }
        });
    }
    $(".form-alert").submit(function (e) {
        e.preventDefault();

        Swal.fire({
            title: "Confirmación!",
            text: "¿Esta seguro de insertar este registro?",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#3085d6',
            cancelButtonColor: '#d33',
            confirmButtonText: "Si, estoy seguro!",
        }).then((result) => {
            if (result.isConfirmed) {
                this.submit();
            }
        })

    });
});

$('#Cancelada').click(function () {
    if ($(this).is(':checked')) {
        $(this).val('true');
        $('.MotivoCancelacion').show();
    } else {
        $('.MotivoCancelacion').hide();
        $("#MotivoCancelacion").val("");
    }
});
