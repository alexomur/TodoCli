using System;
using System.Collections.Generic;
using System.Diagnostics; // BUG_FOR_LAB: неиспользуемый using (Roslyn / Roslynator)
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace TodoCli;

// BUG_FOR_LAB: имя типа с маленькой буквы (StyleCop SA1300)
internal sealed class todoItem
{
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsDone { get; set; }
}

internal static class TodoRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "todo.json");

    public static List<todoItem> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new List<todoItem>();
        }

        var json = File.ReadAllText(FilePath);
        var items = JsonSerializer.Deserialize<List<todoItem>>(json, SerializerOptions);

        // BUG_FOR_LAB: возможен null, но возвращаем в не-nullable тип (CS8603 / Roslyn).
        return items;
    }

    public static void Save(List<todoItem> items)
    {
        var json = JsonSerializer.Serialize(items, SerializerOptions);
        File.WriteAllText(FilePath, json);
    }
}

internal static class Program
{
    // BUG_FOR_LAB_MPROF: искусственная «утечка» для динамического анализа
    private static readonly List<byte[]> LeakBucket = new();

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "add" => RunAdd(rest),
                "list" => RunList(),
                "done" => RunDone(rest),
                "remove" => RunRemove(rest),
                "leak-demo" => RunLeakDemo(), // динамический баг
                _ => UnknownCommand(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHeader(string title)
    {
        // BUG_FOR_LAB: параметр title не используется (Roslynator: RCS* unused parameter)
        Console.WriteLine("Todo CLI");
    }

    private static void PrintUsage()
    {
        PrintHeader("Todo CLI (менеджер задач)");
        Console.WriteLine("  add <text>    - добавить задачу");
        Console.WriteLine("  list          - показать задачи");
        Console.WriteLine("  done <id>     - отметить выполненной");
        Console.WriteLine("  remove <id>   - удалить задачу");
        Console.WriteLine("  leak-demo     - ИСКУССТВЕННАЯ утечка памяти для лабораторной");
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Неизвестная команда: {command}");
        PrintUsage();
        return 1;
    }

    private static int RunAdd(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Текст задачи не задан.");
            return 1;
        }

        // BUG_FOR_LAB: неиспользуемая локальная переменная (CS0219 / IDE0059 / Roslynator)
        int debugNumber = 42;

        var text = string.Join(' ', args);
        var items = TodoRepository.Load();
        var nextId = items.Count == 0 ? 1 : items.Max(t => t.Id) + 1;

        items.Add(new todoItem { Id = nextId, Text = text, IsDone = false });
        TodoRepository.Save(items);

        Console.WriteLine($"OK: добавлена задача #{nextId}");
        return 0;
    }

    private static int RunList()
    {
        var items = TodoRepository.Load();
        if (items.Count == 0)
        {
            Console.WriteLine("Список задач пуст.");
            return 0;
        }

        foreach (var item in items.OrderBy(t => t.Id))
        {
            var status = item.IsDone ? "[x]" : "[ ]";
            Console.WriteLine($"{item.Id,3} {status} {item.Text}");
        }

        return 0;
    }

    private static int RunDone(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var id))
        {
            Console.Error.WriteLine("Нужно указать числовой id задачи.");
            return 1;
        }

        var items = TodoRepository.Load();
        var task = items.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            Console.Error.WriteLine($"Задача #{id} не найдена.");
            return 1;
        }

        task.IsDone = true;
        TodoRepository.Save(items);
        Console.WriteLine($"OK: задача #{id} отмечена выполненной.");
        return 0;
    }

    private static int RunRemove(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var id))
        {
            Console.Error.WriteLine("Нужно указать числовой id задачи.");
            return 1;
        }

        var items = TodoRepository.Load();
        var removed = items.RemoveAll(t => t.Id == id);
        if (removed == 0)
        {
            Console.Error.WriteLine($"Задача #{id} не найдена.");
            return 1;
        }

        TodoRepository.Save(items);
        Console.WriteLine($"OK: задача #{id} удалена.");
        return 0;
    }

    private static int RunLeakDemo()
    {
        Console.WriteLine("Запуск ИСКУССТВЕННОЙ утечки памяти (C#)...");

        for (var i = 0; i < 200; i++)
        {
            // каждый шаг +1 МБ, не освобождаем
            LeakBucket.Add(new byte[1024 * 1024]);
            Thread.Sleep(100);
            Console.WriteLine($"Allocated {LeakBucket.Count} MB");
        }

        Console.WriteLine("Готово. Процесс всё ещё держит выделенную память.");
        Console.WriteLine("Нажмите Enter для выхода...");
        Console.ReadLine();
        return 0;
    }
}
