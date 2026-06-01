# SemiStep

[![CI](https://github.com/Semiteq/SemiStep/actions/workflows/ci.yml/badge.svg)](https://github.com/Semiteq/SemiStep/actions/workflows/ci.yml)
[![Release](https://github.com/Semiteq/SemiStep/actions/workflows/release.yml/badge.svg)](https://github.com/Semiteq/SemiStep/actions/workflows/release.yml)
![C#](https://img.shields.io/badge/C%23-.NET-blue)
![.NET](https://img.shields.io/badge/.NET-10%2B-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows-informational)
![Protocol](https://img.shields.io/badge/Protocol-S7-orange)

SemiStep — самостоятельное Windows-приложение для редактирования и исполнения технологических рецептов на промышленных установках роста и постростовой обработки на базе Siemens PLC. Работает независимо от SCADA-системы и взаимодействует с контроллером напрямую по протоколу S7.

<div align="center">
    <img src=./logo.png width=400 />
</div>

Документация: [Docs/readme.md](./Docs/readme.md)

---

## Возможности

**Редактор рецептов**

- Табличное редактирование с поддержкой отмены/повтора, копирования/вставки и навигации с клавиатуры
- Inline-пересчёт формул: при изменении одной ячейки связанные параметры шага пересчитываются автоматически

**Конфигурация через YAML**

- Набор столбцов, типы параметров, диапазоны значений, типы действий и визуальный стиль задаются в YAML-файлах — никаких изменений в коде для адаптации к новой установке или процессу не требуется
- Каждая установка получает собственную конфигурацию; примеры на скриншотах ниже

**Синхронизация с ПЛК**

- Загрузка рецепта в Siemens PLC автоматически

**Мониторинг исполнения**

- Подсветка текущего исполняемого шага в реальном времени
- Отображение оставшегося времени: до конца текущего шага и до конца рецепта целиком
- Цветовая тонировка строк по глубине вложенности блоков `For`/`End_For`

**Валидация**

- Проверка диапазонов и типов по каждой ячейке; сохранение и отправка в ПЛК заблокированы до устранения ошибок
- Индивидуальное форматирование содержимого ячеек в зависимости от типа данных
- Проверка целостности конфигурации при запуске; некорректный YAML блокирует старт приложения

---

## Примеры конфигураций

YAML-конфигурация определяет внешний вид и поведение приложения для конкретной установки. Скриншоты ниже — три независимые конфигурации для разных типов процессов.

<div align="center">
    <img src="./Docs/img/mocvd_example.png" width="800" />
    <p><em>Конфигурация для MOCVD (металлоорганическая газофазная эпитаксия)</em></p>
</div>

<div align="center">
    <img src="./Docs/img/rie_example.png" width="800" />
    <p><em>Конфигурация для RIE (реактивное ионное травление)</em></p>
</div>

<div align="center">
    <img src="./Docs/img/mbe_example.png" width="800" />
    <p><em>Конфигурация для MBE (молекулярно-лучевая эпитаксия)</em></p>
</div>

---

## Требования

| Компонент    | Требование                                              |
| ------------ | ------------------------------------------------------- |
| ОС           | Windows 10 или Windows 11 (64-bit)                      |
| Среда        | .NET 10                                                 |
| ПЛК          | Siemens S7-1200 / S7-1500, доступный по TCP/IP          |
| Конфигурация | Корректный YAML-файл конфигурации для целевой установки |

---

## Сборка и запуск

```powershell
# Сборка
dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj

# Запуск
dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj

# Тесты
dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj
```
