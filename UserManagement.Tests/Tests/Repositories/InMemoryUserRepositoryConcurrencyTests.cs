using System.Collections.Concurrent;
using UserManagement.API.Models;
using UserManagement.API.Repositories;

namespace UserManagement.Tests.Tests.Repositories;

/// <summary>
/// Набор юнит-тестов для проверки потокобезопасности и целостности данных <see cref="InMemoryUserRepository"/>.
/// Проверяет отсутствие состояний гонки (race conditions), потерь данных и исключений.
/// </summary>
public sealed class InMemoryUserRepositoryConcurrencyTests
{
    /// <summary>
    /// Проверяет параллельное добавление уникальных пользователей без потери данных.
    /// 100 параллельных задач одновременно добавляют уникальных пользователей со сгенерированными Guid v7.
    /// Проверяется одновременный старт через Task.WhenAll, отсутствие исключений,
    /// сохранение ровно 100 элементов и их доступность по <see cref="InMemoryUserRepository.GetById"/>.
    /// </summary>
    [Fact]
    public async Task Add_ConcurrentAddingUsers_ShouldStoreAllUsersWithoutDataLoss()
    {
        // Arrange
        var repository = new InMemoryUserRepository();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        const int userCount = 100;
        var usersToInsert = Enumerable.Range(0, userCount)
            .Select(_ => new User(
                Guid.CreateVersion7(),
                "RU",
                "ru-RU",
                DateTime.UtcNow,
                null))
            .ToArray();

        var addedUsers = new ConcurrentBag<User>();

        var tasks = usersToInsert.Select(user => Task.Run(async () =>
        {
            await startSignal.Task.WaitAsync(cts.Token);
            var res = repository.Add(user);
            addedUsers.Add(res);
        }, cts.Token)).ToArray();

        // Act
        startSignal.SetResult();
        await Task.WhenAll(tasks).WaitAsync(cts.Token);

        // Assert
        addedUsers.Should().BeEquivalentTo(usersToInsert);

        var storedUsers = repository.GetAll().ToList();
        storedUsers.Should().BeEquivalentTo(usersToInsert);

        foreach (var expectedUser in usersToInsert)
        {
            var actualUser = repository.GetById(expectedUser.UserId);
            actualUser.Should().NotBeNull();
            actualUser.Should().Be(expectedUser);
        }
    }

    /// <summary>
    /// Проверяет параллельное добавление одного и того же пользователя.
    /// 50 параллельных задач одновременно вызывают Add с разными экземплярами User, имеющими один и тот же UserId.
    /// Все вызовы должны завершиться без ошибок и вернуть экземпляр, добавленный первым,
    /// а хранилище должно содержать ровно одну запись.
    /// </summary>
    [Fact]
    public async Task Add_ConcurrentAddingUsersWithSameGuid_ShouldReturnExistingAndHaveSingleEntry()
    {
        // Arrange
        var repository = new InMemoryUserRepository();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var sharedUserId = Guid.CreateVersion7();
        const int userCount = 50;
        var candidateUsers = Enumerable.Range(0, userCount)
            .Select(i => new User(
                sharedUserId,
                "US",
                $"en-US-{i:D2}",
                DateTime.UtcNow,
                null))
            .ToArray();

        var returnedUsers = new ConcurrentBag<User>();

        var tasks = candidateUsers.Select(candidate => Task.Run(async () =>
        {
            await startSignal.Task.WaitAsync(cts.Token);
            var res = repository.Add(candidate); // если пользователь уже есть - не добавляет, а возвращает уже добавленного!
            returnedUsers.Add(res);
        }, cts.Token)).ToArray();

        // Act
        startSignal.SetResult();
        await Task.WhenAll(tasks).WaitAsync(cts.Token);

        // Assert
        returnedUsers.Should().HaveCount(userCount);
        var canonicalUser = repository.GetById(sharedUserId);
        canonicalUser.Should().NotBeNull();
        returnedUsers.Should().AllSatisfy(user => user.Should().Be(canonicalUser));

        var allUsers = repository.GetAll().ToList();
        allUsers.Should().ContainSingle();
        allUsers.Should().AllSatisfy(user => user.Should().Be(canonicalUser));
    }

    /// <summary>
    /// Параллельное обновление одного и того же пользователя.
    /// 100 параллельных задач одновременно вызывают Update для одного существующего пользователя
    /// с разными значениями локали.
    /// Все вызовы должны завершиться успешно (true), а финальное состояние пользователя
    /// должно оставаться валидным, целостным и неповрежденным.
    /// </summary>
    [Fact]
    public async Task Update_ConcurrentUpdatesSameUser_ShouldUpdateWithoutDataLoss()
    {
        // Arrange
        var repository = new InMemoryUserRepository();
        var initialUser = new User(
            Guid.CreateVersion7(),
            "US",
            "en-US",
            DateTime.UtcNow,
            null);
        repository.Add(initialUser);

        const int updateCount = 100;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var updateResults = new ConcurrentBag<bool>();
        var validLocales = Enumerable.Range(0, updateCount)
            .Select(i => $"en-{i:D2}")
            .ToHashSet();

        var tasks = Enumerable.Range(0, updateCount).Select(i => Task.Run(async () =>
        {
            var updatedUser = initialUser with
            {
                Locale = $"en-{i:D2}",
                DateModified = DateTime.UtcNow
            };

            await startSignal.Task.WaitAsync(cts.Token);

            var success = repository.Update(updatedUser);
            updateResults.Add(success);
        }, cts.Token)).ToArray();

        // Act
        startSignal.SetResult();
        await Task.WhenAll(tasks).WaitAsync(cts.Token);

        // Assert
        updateResults.Should().HaveCount(updateCount);
        updateResults.Should().AllSatisfy(success => success.Should().BeTrue());

        var actualUser = repository.GetById(initialUser.UserId);
        actualUser.Should().NotBeNull();
        actualUser.UserId.Should().Be(initialUser.UserId);
        actualUser.CountryIsoCode.Should().Be(initialUser.CountryIsoCode);
        actualUser.DateModified.Should().NotBeNull();

        validLocales.Should().Contain(actualUser.Locale);

        repository.GetAll().Should().ContainSingle();
    }

    /// <summary>
    /// Одновременное чтение и запись.
    /// 50 потоков непрерывно читают данные (GetAll().ToList(), GetById()), пока другие 50 потоков
    /// параллельно добавляют новых пользователей и обновляют существующих.
    /// Ни одна операция чтения или записи не должна выбрасывать исключений,
    /// а хранилище должно сохранить целостность данных.
    /// </summary>
    [Fact]
    public async Task MixedReadWrite_ConcurrentOperations_ShouldNotThrow()
    {
        // Arrange
        var repository = new InMemoryUserRepository();
        const int readerCount = 50;
        const int writerCount = 50;
        const int operationsPerWriter = 40;

        var seedUsers = Enumerable.Range(0, 20)
            .Select(i => new User(
                Guid.CreateVersion7(),
                "RU",
                $"ru-{i:D2}",
                DateTime.UtcNow,
                null))
            .ToArray();

        foreach (var seedUser in seedUsers)
        {
            repository.Add(seedUser);
        }

        var knownUserIds = new ConcurrentQueue<Guid>(seedUsers.Select(u => u.UserId));
        var exceptions = new ConcurrentBag<Exception>();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var writerCompletionCts = new CancellationTokenSource();
        using var readerCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, writerCompletionCts.Token);

        // Токен читателя состоит из общего таймаута + токена писателя.
        // Писатель там нужен, потому что пытаться писать 
        var readerToken = readerCts.Token;
        var writerToken = writerCompletionCts.Token;
        
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 50 потоков параллельно читают записи
        var readerTasks = Enumerable.Range(0, readerCount).Select(_ => Task.Run(async () =>
        {
            try
            {
                await startSignal.Task.WaitAsync(timeoutCts.Token);

                while (!readerToken.IsCancellationRequested)
                {
                    var all = repository.GetAll().ToList();
                    all.Should().HaveCountGreaterThanOrEqualTo(seedUsers.Length);

                    var snapshotKeys = knownUserIds.ToArray();
                    var userId = snapshotKeys[Random.Shared.Next(snapshotKeys.Length)];
                    var user = repository.GetById(userId);
                    user.Should().NotBeNull($"пользователь с ID {userId} обязан присутствовать в хранилище");

                    var nonExistent = repository.GetById(Guid.Empty);
                    nonExistent.Should().BeNull();

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (writerToken.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
            {
                // Ожидаемое завершение цикла чтения после завершения работы пишущих потоков
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }, timeoutCts.Token)).ToArray();

        // 50 потоков параллельно создают новые записи и обновляют существующие
        var writerTasks = Enumerable.Range(0, writerCount).Select(writerIndex => Task.Run(async () =>
        {
            try
            {
                await startSignal.Task.WaitAsync(timeoutCts.Token);

                for (var i = 0; i < operationsPerWriter && !timeoutCts.Token.IsCancellationRequested; i++)
                {
                    var newUser = new User(
                        Guid.CreateVersion7(),
                        "KZ",
                        $"kz-{writerIndex:D2}",
                        DateTime.UtcNow,
                        null);

                    repository.Add(newUser);
                    knownUserIds.Enqueue(newUser.UserId);

                    var snapshot = knownUserIds.ToArray();
                    var existingId = snapshot[Random.Shared.Next(snapshot.Length)];
                    var currentUser = repository.GetById(existingId);
                    if (currentUser == null)
                    {
                        exceptions.Add(new InvalidOperationException($"Пользователь {existingId} не найден в хранилище"));
                        continue;
                    }

                    var updatedUser = currentUser with
                    {
                        Locale = $"up-{writerIndex:D2}",
                        DateModified = DateTime.UtcNow
                    };

                    if (!repository.Update(updatedUser))
                        exceptions.Add(new InvalidOperationException($"Не удалось обновить пользователя {existingId}"));

                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }, timeoutCts.Token)).ToArray();

        // Act
        startSignal.SetResult();

        // Ждем, пока писатели сами выполнят все свои операции(40 штук).
        // Читатели в это время непрерывно читают(ради этого тест и нужен).
        // timeoutCts тут аварийная защита, если что-то зависло, тест упадет через 15 с.
        await Task.WhenAll(writerTasks).WaitAsync(timeoutCts.Token);

        // Если мы тут - писатели закончили, значит, читателям больше нечего проверять.
        // У читателей бесконечный цикл, поэтому останавливаем их отменой токена.
        await writerCompletionCts.CancelAsync();

        // Дожидаемся, пока читатели выйдут из цикла, чтобы все их исключения попали в список.
        await Task.WhenAll(readerTasks).WaitAsync(timeoutCts.Token);

        // Assert
        exceptions.Should().BeEmpty();

        var finalUsers = repository.GetAll().ToList();
        finalUsers.Should().NotBeEmpty();

        var expectedTotalUsers = seedUsers.Length + writerCount * operationsPerWriter;
        finalUsers.Should().HaveCount(expectedTotalUsers);

        foreach (var user in finalUsers)
        {
            var userFromRepo = repository.GetById(user.UserId);
            userFromRepo.Should().NotBeNull();
            userFromRepo.Should().Be(user);
        }
    }
}
