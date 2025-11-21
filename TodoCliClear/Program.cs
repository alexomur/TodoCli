namespace TodoCliClear;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

internal sealed class TodoItem
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

    public static List<TodoItem> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new List<TodoItem>();
        }

        var json = File.ReadAllText(FilePath);
        var items = JsonSerializer.Deserialize<List<TodoItem>>(json, SerializerOptions);

        return items ?? new List<TodoItem>();
    }

    public static void Save(List<TodoItem> items)
    {
        var json = JsonSerializer.Serialize(items, SerializerOptions);
        File.WriteAllText(FilePath, json);
    }
}

internal static class Program
{
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
                "leak-demo" => RunLeakDemo(), // мок для динамического анализа
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
        Console.WriteLine(title);
    }

    private static void PrintUsage()
    {
        PrintHeader("Todo CLI (менеджер задач)");
        Console.WriteLine("  add <text>    - добавить задачу");
        Console.WriteLine("  list          - показать задачи");
        Console.WriteLine("  done <id>     - отметить выполненной");
        Console.WriteLine("  remove <id>   - удалить задачу");
        Console.WriteLine("  leak-demo     - тестовая команда (без реальной утечки)");
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

        var text = string.Join(' ', args);
        var items = TodoRepository.Load();
        var nextId = items.Count == 0 ? 1 : items.Max(t => t.Id) + 1;

        items.Add(new TodoItem { Id = nextId, Text = text, IsDone = false });
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
        Console.WriteLine("Запуск leak-demo (чистая версия, без реальной утечки)...");

        for (var i = 0; i < 200; i++)
        {
            // ВАЖНО: буфер локальный, не кладём его в список — утечки нет.
            var buffer = new byte[1024 * 1024]; // 1 MB временный буфер
            Console.WriteLine($"Временный буфер #{i + 1} размером 1 MB создан.");
            Thread.Sleep(100);
        }

        Console.WriteLine("Готово. Буферы больше не используются и могут быть собраны GC.");
        Console.WriteLine("Нажмите Enter для выхода...");
        Console.ReadLine();

        return 0;
    }

}
