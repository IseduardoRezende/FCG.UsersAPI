# Testes — FCG.UsersAPI

Este arquivo lista os cenários de teste do microsserviço de usuários. Os nomes seguem o padrão `Metodo_Cenario_ResultadoEsperado`.

| Suíte | Projeto | Ferramentas | Execuções |
|-------|---------|-------------|-----------|
| Unitários | `05- Tests/FCG.Users.UnitTests` | xUnit v3, Moq, FluentValidation.TestHelper | 39 |
| Integração | `05- Tests/FCG.Users.IntegrationTests` | WebApplicationFactory, Testcontainers (PostgreSQL 17), MassTransit Test Harness | 13 |

A contagem de execuções inclui cada caso de `[Theory]`.

## Como executar

```powershell
dotnet test --project "05- Tests/FCG.Users.UnitTests/FCG.Users.UnitTests.csproj"
dotnet test --project "05- Tests/FCG.Users.IntegrationTests/FCG.Users.IntegrationTests.csproj"   # requer Docker em execução
```

---

## Testes unitários

### `UserService` (cadastro, login, consulta, edição e exclusão)

| Teste | Regra de negócio verificada |
|-------|-----------------------------|
| `RegisterAsync_WithValidData_PersistsUserWithDefaultUserRole` | Todo novo cadastro recebe a role `User`. |
| `RegisterAsync_WithValidData_PublishesUserCreatedEventWithIdNameAndEmail` | Um cadastro válido publica o `UserCreatedEvent` com o id, o nome e o e-mail, que o Notifications usa no e-mail de boas-vindas. |
| `RegisterAsync_WithMixedCaseEmailAndPaddedName_NormalizesBeforePersisting` | O e-mail é gravado em minúsculas e o nome sem espaços extras. |
| `RegisterAsync_WithValidData_StoresHashedPasswordAndNeverPlainText` | A senha é gravada como hash com salt, nunca em texto puro. |
| `RegisterAsync_WithAlreadyRegisteredEmail_ReturnsConflictAndDoesNotPublishEvent` | Um e-mail duplicado retorna `Conflict`, sem persistir e sem publicar evento. |
| `RegisterAsync_WithWeakPassword_ReturnsInvalidAndDoesNotPersistNorPublish` | Uma senha fraca bloqueia o cadastro, sem persistir e sem publicar evento. |
| `LoginAsync_WithValidCredentials_ReturnsToken` | Credenciais corretas geram um JWT. |
| `LoginAsync_WithWrongPassword_ReturnsNotFoundWithoutGeneratingToken` | Com a senha errada nenhum token é emitido. |
| `LoginAsync_WithUnknownEmail_ReturnsNotFound` | Um e-mail inexistente retorna `NotFound`. |
| `LoginAsync_WithInvalidEmailFormat_ReturnsInvalid` | Um e-mail inválido é rejeitado antes de consultar o banco. |
| `GetByIdAsync_AsAdministrator_ReturnsAnyUser` | O administrador consulta qualquer usuário. |
| `GetByIdAsync_AsTheUserHimself_ReturnsOwnData` | O usuário consulta os próprios dados. |
| `GetByIdAsync_AsAnotherNonAdminUser_ReturnsForbiddenWithoutQueryingRepository` | Um usuário comum não consulta outros usuários e recebe 403. |
| `GetByIdAsync_WhenUserDoesNotExist_ReturnsNotFound` | Um usuário inexistente retorna `NotFound`. |
| `UpdateAsync_WithValidData_UpdatesNameEmailAndRole` | A edição atualiza o nome, o e-mail (normalizado) e a role. |
| `UpdateAsync_WithEmailOwnedByAnotherUser_ReturnsConflict` | Não é possível usar o e-mail de outro usuário. |
| `UpdateAsync_KeepingOwnEmail_DoesNotReturnConflict` | Manter o próprio e-mail não gera conflito. |
| `UpdateAsync_WithNonexistentRole_ReturnsInvalid` | A role informada precisa existir. |
| `UpdateAsync_WhenUserDoesNotExist_ReturnsNotFound` | Editar um usuário inexistente retorna `NotFound`. |
| `DeleteAsync_WhenUserExists_MarksUserAsDeletedInsteadOfRemoving` | A exclusão é lógica (soft delete). |
| `DeleteAsync_WhenUserDoesNotExist_ReturnsNotFound` | Excluir um usuário inexistente retorna `NotFound`. |

### `TokenService`

| Teste | Regra verificada |
|-------|------------------|
| `Generate_ForUser_IncludesClaimsRequiredByOtherMicroservices` | O JWT contém `sub`, `email`, `role` e `name`. Os outros microsserviços dependem dessas claims; o Catalog, por exemplo, usa o e-mail no pedido. |
| `Generate_ForUser_UsesConfiguredIssuerAndAudience` | O issuer e a audience são os mesmos que os outros serviços validam. |
| `Generate_ForUser_ExpiresAfterConfiguredDays` | A expiração respeita `TokenSettings:DaysUntilExpires`. |

### `UserRoleService`

| Teste | Regra verificada |
|-------|------------------|
| `GetAllAsync_WithSeededRoles_ReturnsUserAndAdministrator` | Lista as roles do seed, `User` e `Administrator`. |

### `RegisterUserDtoValidator`

| Teste | Regra verificada |
|-------|------------------|
| `Validate_WithAllFieldsValid_HasNoErrors` | Um DTO válido passa. |
| `Validate_WithEmptyName_HasNameError` | O nome é obrigatório. |
| `Validate_WithNameLongerThan100Characters_HasNameError` | O nome tem no máximo 100 caracteres. |
| `Validate_WithInvalidEmail_HasEmailError` (3 casos) | O e-mail é obrigatório e precisa ter formato válido. |
| `Validate_WithPasswordNotMeetingSecurityPolicy_HasPasswordError` (4 casos) | A senha precisa de no mínimo 8 caracteres, com letra, número e caractere especial. |

### `PasswordHasher`

| Teste | Regra verificada |
|-------|------------------|
| `Verify_WithSamePasswordAndSalt_ReturnsTrue` | O hash PBKDF2 é verificável. |
| `Verify_WithWrongPassword_ReturnsFalse` | Uma senha diferente não é aceita. |
| `GenerateSalt_CalledTwice_ProducesDifferentSalts` | Cada usuário recebe um salt único. |
| `Verify_WithDocumentedDefaultAdminPassword_MatchesSeededHash` | A senha documentada do admin (`Fcg@Admin2026!`) corresponde ao hash do seed. |

---

## Testes de integração

Os testes sobem a API real com `WebApplicationFactory` e aplicam as migrations em um PostgreSQL efêmero criado pelo Testcontainers. O RabbitMQ é substituído pelo transporte em memória do MassTransit Test Harness, o que permite verificar os eventos publicados.

| Teste | Cenário ponta a ponta |
|-------|-----------------------|
| `Register_WithValidData_Returns201AndPersistsUserInDatabase` | `POST /register` grava o usuário no banco com a role `User` e a senha em hash. |
| `Register_WithValidData_PublishesUserCreatedEventToTheBroker` | O cadastro publica o `UserCreatedEvent` no broker. |
| `Register_WithAlreadyRegisteredEmail_Returns409` | Um e-mail duplicado retorna 409, mesmo com maiúsculas e minúsculas diferentes. |
| `Register_WithWeakPassword_Returns422` | Uma senha fraca retorna 422. |
| `Login_WithSeededAdministrator_ReturnsJwtWithAdministratorRoleAndEmail` | O admin do seed autentica e o JWT traz a role e o e-mail. |
| `Login_WithWrongPassword_Returns404` | Credenciais inválidas retornam 404. |
| `ListUsers_WithoutToken_Returns401` | A listagem exige autenticação. |
| `ListUsers_WithCommonUserToken_Returns403` | A listagem é exclusiva do administrador. |
| `ListUsers_WithAdministratorToken_Returns200` | O administrador lista os usuários. |
| `GetUserById_ForAnotherUserWithCommonToken_Returns403` | Um usuário comum não acessa outro usuário. |
| `GetUserById_ForOwnUser_Returns200WithOwnData` | O usuário acessa os próprios dados. |
| `DeleteUser_AsAdministrator_SoftDeletesAndBlocksFutureLogin` | Depois do soft delete o usuário não consegue mais logar. |
| `Health_WhenDatabaseIsReachable_Returns200` | O `/health` usado pelas probes do Kubernetes responde OK. |
