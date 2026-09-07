# Batch 2 UI/DPI acceptance evidence

Date: 2026-09-07

## Automated evidence

- `Rah_Negar.Tests/UI/Batch2UiAcceptanceTests.cs` passes the supported scale matrix (100%, 125%, and 150%) and rejects 175% through the existing policy.
- `UI/Forms/Base/BaseForm.cs` applies the common RTL/RTL-layout and baseline control policy to user-facing forms and blocks unsupported DPI with a Persian message.
- `Services/UI/UiMessageService.cs` uses Persian default titles and logs technical exception details instead of presenting raw exception text to operators.

## Acceptance limitation

The current execution environment exposes no native WinForms desktop surface or screenshot-capable Windows UI automation provider. Therefore no visual PASS is claimed for 1920x1080 form-by-form inspection. Manual observations still required are clipping/overlap, keyboard focus and tab order, font substitution, grid readability, RTL rendering, and dialog placement at 100/125/150% for all user-facing forms.

H-05 remains open until those observations and retained screenshots are completed by an operator on a real desktop surface.
