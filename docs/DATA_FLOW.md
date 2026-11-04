# Data flow — platform-level example

This conceptual example is a starting point. KT1 requires each student to provide a separate DFD for their own component, including its actual processes, actors, and stores.

```mermaid
flowchart LR
  Student[Student] -->|Lab request| API[API and server-side authorization]
  API -->|Check context| Access[Identity and authorization]
  Access -->|Allow / deny| API
  API -->|Authorized revision| Catalog[(Published revisions)]
  API -->|Save response| Attempt[(Attempts)]
  Attempt -->|Response snapshot| Eval[Evaluation]
  Eval -->|Outcome and feedback| Result[(Result history)]
  API -->|Security event| Audit[(Audit log)]
  Result -->|Authorized view| Student
```

## Identity login flow (IDN-02 / IDN-03)

```mermaid
flowchart LR
  User[User] -->|Email and password| Login[LoginController]
  Login --> Verify[ICredentialAuthenticator, IDN-02]
  Verify -->|Invalid or unavailable| Reject[Generic rejection]
  Verify -->|Verified account id| Status[LoginAccount status gate, IDN-03]
  Status -->|Read current status| Accounts[(IAccountRepository)]
  Status -->|BLOCKED / ARCHIVED| Message[Status-specific 403 response]
  Status -->|ACTIVE| Issue[ISessionIssuer, IDN-02]
  Issue -->|Session token| Login
  Reject --> Login
  Message --> Login
```

The API checks status only after credentials are valid. The current credential
and session adapters fail closed until IDN-02 replaces them.

## Component DFD requirements

1. Input and output data, external actors, and request initiators.
2. Processes and the stores they access.
3. Trust boundaries and server-side authorization checks.
4. Negative flows and handling of sensitive data.
5. The distinction between implemented and planned behavior.
