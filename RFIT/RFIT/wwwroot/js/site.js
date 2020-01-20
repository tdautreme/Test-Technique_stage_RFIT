// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// --------------------------------------------------------------------------------------------------------

// Misc part

function materialWasEdited(id) { // If material was edited, the save button appear
    document.getElementById("material_saveBtn_" + id).style.display = "inline";
}

function dateToTimestamp(strDate) { // Function for convert date to timestamp (database stock date in timestamp format)
    var datum = Date.parse(strDate);
    return datum / 1000;
}

function getMaterialInfo(id) { // Function to get material info for Edit and Add request
    var infos = {
        name: document.getElementById("material_name_" + id).value,
        serialNumber: document.getElementById("material_serialNumber_" + id).value,
        inspectionDate: dateToTimestamp(
            document.getElementById("material_date_" + id).value + " " +
            document.getElementById("material_time_" + id).value),
    };
    if (id != "add")
        infos.id = id;
    else
        infos.imagePath = null;
    return infos;
}

function useFilter(key) { // Function for filter materials with name or serial number
    var materials = document.getElementsByClassName("material");
    for (var i = 0; i < materials.length; ++i) {
        var material = materials[i];
        var materialName = document.getElementById("material_name_" + material.id).value;
        var materialSerialNumber = document.getElementById("material_serialNumber_" + material.id).value;
        if (materialName.includes(key) || materialSerialNumber.includes(key))
            material.style.display = "block";
        else
            material.style.display = "none";
    }
}

function messageManager(result, afterFnc = null, afterArg = null) { // Function for show error / success messages with alert
    messages = result['messages'];
    if (messages.length > 0) {
        var alertStr = "";
        for (var i = 0; i < messages.length; ++i)
            alertStr += "<div>" + messages[i] + "</div>";
        var alertFnc = result['isError'] ? myAlertError : myAlertSuccess;
        alertFnc(alertStr, afterFnc, afterArg);
    }
    else if (afterFnc != null) {
        afterFnc(afterArg);
    }
}

function myModalCtrl(title, message) { // Called by MyAlert and MyConfirm
    $('#modal-title').html(title);
    $('#modal-body').html(message);
    $('#myModal').modal("show");
}

function myConfirm(title, message, okFnc) { // Confirm popup
    title = "<div style='color: #FF851B;'>" + title + "</div>";
    $('#modal-button-ok').unbind();
    $('#modal-button-ok').click(okFnc);
    $('#modal-button-ok').show();
    myModalCtrl(title, message);
}

function myAlert(title, message, afterFnc = null, afterArg = null) { // Alert popup
    $('#modal-button-ok').hide();
    if (afterFnc != null) {
        $('#modal-button-close_1').unbind();
        $('#modal-button-close_2').unbind();
        $('#modal-button-close_1').click({ fnc: afterFnc, param: afterArg }, function (event) {
            event.data.fnc(event.data.param);     
        });
        $('#modal-button-close_2').click({ fnc: afterFnc, param: afterArg }, function (event) {
            event.data.fnc(event.data.param);
        });
    }
    myModalCtrl(title, message);
}

function myAlertSuccess(message, afterFnc = null, afterArg = null) { // Alert success extension
    var title = "<span class='nice-green' >" + translate.success[lang] + "</span>";
    myAlert(title, message, afterFnc, afterArg);
}

function myAlertError(message, afterFnc = null, afterArg = null) { // Alert error extension
    var title = "<span class='nice-red'> " + translate.error[lang] + " </span>";
    myAlert(title, message, afterFnc, afterArg);
}

// Change language

function setLanguage(lang) {
    $.ajax({
        // This send material model to any controller
        type: "POST",
        url: "Home/SetLanguage",
        data: JSON.stringify(lang),
        contentType: "application/json"
    })
    .done(function (result) {
        // All controller send result with data
        messageManager(result); // Error / Success messages
        if (!result['isError'])
            location.reload();
    });
}


// Request part

function tryAddMaterial() { // Function for try adding material
    sendAjaxRequest("Home/AddMaterial", getMaterialInfo("add"));
}

function tryEditMaterial(id) { // Function for try editing material
    sendAjaxRequest("Home/EditMaterial", getMaterialInfo(id));
}

function tryDeleteMaterial(id) { // Function for try deleting material
    myConfirm("Delete", translate.deleteConfirm[lang], function () {
        sendAjaxRequest("Home/DeleteMaterial", { id: id });
    });
}

// Response part

function resAddMaterial(result) { // Function which control UI with response of Add request
    if (!result["isError"]) {

        // This send image only if material has been added successfully
        sendAjaxFileRequest("/Home/AddImage", result["material"].id, function (result_2) {
            if (result_2 != null) { // If need send image
                // All controller send result with data
                messageManager(result_2, function (result_3) { // Error / Success messages
                    location.reload();
                }, result_2);
            }
            else // If no image to send
                location.reload();
        }, "add");
    }
}

function resEditMaterial(result) { // Function which control UI with response of Edit request
    var material = result['material'];
    document.getElementById("material_name_" + material["id"]).innerHTML = material["name"];
    document.getElementById("material_serialNumber_" + material["id"]).innerHTML = material["serialNumber"];
    if (!result['isError']) {
        document.getElementById("material_saveBtn_" + material["id"]).style.display = "none";

        // This update image only if material has been updated successfully

        sendAjaxFileRequest("/Home/AddImage", result["material"].id, function (result_2) {
            if (result_2 != null) { // If need send image
                // All controller send result with data
                messageManager(result_2, function (result_3) {
                    if (result_3["material"] != null)
                        document.getElementById("material_image_" + result_3["material"]["id"]).src = result_3["material"]["imagePath"] + "?r=" + new Date().getTime();
                }, result_2); // Error / Success messages
            }
        });
    }
}

function resDeleteMaterial(result) { // Function which control UI with response of Delete request
    var material = result['material'];
    document.getElementById(material["id"]).remove();
}

// Req / Res controller

resFnc = { "add": resAddMaterial, "edit": resEditMaterial, "delete": resDeleteMaterial }; // This is the response functions router

function sendAjaxRequest(url, model) { // Ajax request controller
    $.ajax({
        // This send material model to any controller
        type: "POST",
        url: url,
        data: JSON.stringify(model),
        contentType: "application/json"
    })
    .done(function (result) {
        // All controller send result with data
        messageManager(result, function (result) {
            resFnc[result["reqType"]](result); // After showing messages if not empty, use function with router.
        }, result); // Error / Success messages
    });
}

function sendAjaxFileRequest(url, id, doneFnc, specialReq) {
    specialReq = specialReq == undefined ? id : specialReq;
    var inputImage = document.getElementById("material_imageInput_" + specialReq);
    var files = inputImage.files;
    if (files.length > 0) {
        var formData = new FormData();
        formData.append(files[0].name, files[0]);
        formData.append('id', id);
        try {
            $.ajax({
                // This send image and material id
                url: url,
                type: 'POST',
                data: formData,
                contentType: false,
                processData: false,
            })
                .done(doneFnc);
        }
        catch {
            if (doneFnc != null)
                doneFnc();
        }
    }
    else if (doneFnc != null)
        doneFnc();
}