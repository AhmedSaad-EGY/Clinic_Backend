using Clinic.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Infrastructure.Errors;

public static class ControllerResultExtensions
{
    public static ActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? controller.NoContent()
            : CreateProblem(controller, result.Error);
    }

    public static ActionResult<TValue> ToActionResult<TValue>(
        this ControllerBase controller,
        Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? controller.Ok(result.Value)
            : CreateProblem(controller, result.Error);
    }

    private static ObjectResult CreateProblem(
        ControllerBase controller,
        ResultError error)
    {
        int statusCode = GetStatusCode(error.Code);
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Description,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        if (error.Extensions is not null)
        {
            foreach ((string key, object? value) in error.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        return controller.StatusCode(statusCode, problemDetails);
    }

    private static int GetStatusCode(string errorCode) => errorCode switch
    {
        "identity.not_authenticated" => StatusCodes.Status401Unauthorized,
        "identity.invalid_credentials" => StatusCodes.Status401Unauthorized,
        "identity.account_locked" => StatusCodes.Status423Locked,
        "identity.forbidden" => StatusCodes.Status403Forbidden,
        "identity.account_disabled" => StatusCodes.Status403Forbidden,
        "identity.user_not_found" => StatusCodes.Status404NotFound,
        "identity.duplicate_username" => StatusCodes.Status409Conflict,
        "identity.duplicate_phone" => StatusCodes.Status409Conflict,
        "identity.duplicate_email" => StatusCodes.Status409Conflict,
        "catalog.not_authenticated" => StatusCodes.Status401Unauthorized,
        "catalog.department_not_found" => StatusCodes.Status404NotFound,
        "catalog.specialization_not_found" => StatusCodes.Status404NotFound,
        "catalog.service_not_found" => StatusCodes.Status404NotFound,
        "catalog.device_not_found" => StatusCodes.Status404NotFound,
        "catalog.duplicate_name" => StatusCodes.Status409Conflict,
        "catalog.duplicate_identifier" => StatusCodes.Status409Conflict,
        "catalog.concurrency_conflict" => StatusCodes.Status409Conflict,
        "catalog.dependency_conflict" => StatusCodes.Status409Conflict,
        "scheduling.not_authenticated" => StatusCodes.Status401Unauthorized,
        "scheduling.doctor_not_found" => StatusCodes.Status404NotFound,
        "scheduling.schedule_not_found" => StatusCodes.Status404NotFound,
        "scheduling.exception_not_found" => StatusCodes.Status404NotFound,
        "scheduling.closure_not_found" => StatusCodes.Status404NotFound,
        "scheduling.department_not_found" => StatusCodes.Status404NotFound,
        "scheduling.service_not_found" => StatusCodes.Status404NotFound,
        "scheduling.concurrency_conflict" => StatusCodes.Status409Conflict,
        "scheduling.conflict" => StatusCodes.Status409Conflict,
        "patients.not_authenticated" => StatusCodes.Status401Unauthorized,
        "patients.patient_not_found" => StatusCodes.Status404NotFound,
        "patients.note_not_found" => StatusCodes.Status404NotFound,
        "patients.treatment_history_not_found" => StatusCodes.Status404NotFound,
        "patients.duplicate_primary_phone" => StatusCodes.Status409Conflict,
        "patients.concurrency_conflict" => StatusCodes.Status409Conflict,
        "appointments.not_authenticated" => StatusCodes.Status401Unauthorized,
        "appointments.not_found" => StatusCodes.Status404NotFound,
        "appointments.patient_not_found" => StatusCodes.Status404NotFound,
        "appointments.department_not_found" => StatusCodes.Status404NotFound,
        "appointments.resource_not_found" => StatusCodes.Status404NotFound,
        "appointments.follow_up_not_found" => StatusCodes.Status404NotFound,
        "appointments.conflict" => StatusCodes.Status409Conflict,
        "appointments.concurrency_conflict" => StatusCodes.Status409Conflict,
        "appointments.not_editable" => StatusCodes.Status409Conflict,
        "cashier.not_authenticated" => StatusCodes.Status401Unauthorized,
        "cashier.forbidden" => StatusCodes.Status403Forbidden,
        "cashier.drawer_not_found" => StatusCodes.Status404NotFound,
        "cashier.shift_not_found" => StatusCodes.Status404NotFound,
        "cashier.policy_not_found" => StatusCodes.Status404NotFound,
        "cashier.payment_not_found" => StatusCodes.Status404NotFound,
        "cashier.approval_request_not_found" => StatusCodes.Status404NotFound,
        "cashier.refund_not_found" => StatusCodes.Status404NotFound,
        "cashier.payment_method_not_found" => StatusCodes.Status404NotFound,
        "cashier.conflict" => StatusCodes.Status409Conflict,
        "cashier.concurrency_conflict" => StatusCodes.Status409Conflict,
        "cashier.reconciliation_stale" => StatusCodes.Status409Conflict,
        "cashier.appointment_not_payable" => StatusCodes.Status409Conflict,
        "cashier.patient_package_not_payable" => StatusCodes.Status409Conflict,
        "cashier.idempotency_conflict" => StatusCodes.Status409Conflict,
        "cashier.approval_required" => StatusCodes.Status409Conflict,
        "clinical.not_authenticated" => StatusCodes.Status401Unauthorized,
        "clinical.prescription_not_found" => StatusCodes.Status404NotFound,
        "clinical.appointment_service_not_found" => StatusCodes.Status404NotFound,
        "clinical.follow_up_not_found" => StatusCodes.Status404NotFound,
        "clinical.patient_not_found" => StatusCodes.Status404NotFound,
        "clinical.concurrency_conflict" => StatusCodes.Status409Conflict,
        "clinical.conflict" => StatusCodes.Status409Conflict,
        "packages.not_authenticated" => StatusCodes.Status401Unauthorized,
        "packages.not_found" => StatusCodes.Status404NotFound,
        "packages.department_not_found" => StatusCodes.Status404NotFound,
        "packages.service_not_found" => StatusCodes.Status404NotFound,
        "packages.duplicate_name" => StatusCodes.Status409Conflict,
        "packages.concurrency_conflict" => StatusCodes.Status409Conflict,
        "patient_packages.patient_not_found" => StatusCodes.Status404NotFound,
        "patient_packages.not_found" => StatusCodes.Status404NotFound,
        "patient_packages.definition_unavailable" => StatusCodes.Status409Conflict,
        "patient_packages.definition_incomplete" => StatusCodes.Status409Conflict,
        "patient_packages.idempotency_conflict" => StatusCodes.Status409Conflict,
        "discounts.not_authenticated" => StatusCodes.Status401Unauthorized,
        "discounts.not_found" => StatusCodes.Status404NotFound,
        "discounts.target_not_found" => StatusCodes.Status404NotFound,
        "discounts.overlap" => StatusCodes.Status409Conflict,
        "discounts.concurrency_conflict" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
