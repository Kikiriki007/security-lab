# LAB-06 – Prerequisite Access Control

## 1. Purpose

The purpose of this component is to prevent students from starting a laboratory when its required prerequisite laboratories have not been completed.

Access is checked on the server side. The client interface does not make access-control decisions.

## 2. Main components

* `PrerequisiteAccessService` – checks whether all required laboratories have been completed.
* `IPrerequisiteRepository` – retrieves prerequisites for a laboratory.
* `ICompletedLaboratories` – retrieves laboratories completed by a student.
* `LaboratoryPrerequisiteAccessAdapter` – connects assignment starting with prerequisite verification.
* `StartAttemptService` – rejects an attempt if prerequisites are not fulfilled.

## 3. Security flow

1. A student requests to start an assignment.
2. `StartAttemptService` checks the student's identity and authorization.
3. `LaboratoryPrerequisiteAccessAdapter` resolves the laboratory revision.
4. `PrerequisiteAccessService` checks the laboratory prerequisites.
5. If all prerequisites are fulfilled, the request may continue.
6. Otherwise, the request is rejected and no attempt is created.

## 4. Threat – Control – Test – Evidence

| Threat                                                               | Control                               | Test                                                | Evidence                                                  |
| -------------------------------------------------------------------- | ------------------------------------- | --------------------------------------------------- | --------------------------------------------------------- |
| Student directly calls the start-attempt API for a locked laboratory | Server-side prerequisite check        | `StartAttempt_WhenPrerequisitesNotFulfilled_Throws` | Unauthorized access exception; attempt count remains zero |
| Student has not completed a required laboratory                      | `PrerequisiteAccessService`           | Unmet prerequisites test                            | `IsUnlocked` is false                                     |
| Laboratory revision does not exist                                   | `LaboratoryPrerequisiteAccessAdapter` | `CanStart_WhenRevisionDoesNotExist_ReturnsFalse`    | Access is denied                                          |
| Student ID is invalid                                                | Adapter input validation              | `CanStart_WhenStudentIdIsInvalid_ReturnsFalse`      | Access is denied                                          |
| All prerequisites are fulfilled                                      | Server-side prerequisite check        | `StartAttempt_WhenPrerequisitesFulfilled_Succeeds`  | Attempt is created                                        |

## 5. Current limitations

`StubCompletedLaboratories` is currently used as a temporary implementation of completed-laboratory data.

A production implementation must use the actual student completion records.

## 6. Demonstration

Two scenarios are demonstrated:

* A student who fulfilled all prerequisites can start the assignment.
* A student who did not fulfill the prerequisites is denied access, even when the start request is sent directly without using the user interface.
