# Документация на протокол передачи (S7)

## Определения

**Датаблок (DB)** — блок данных Siemens, используется как единица хранения данных в PLC.

**Транзакция / bulk update** — логическая операция массовой записи данных (например загрузка рецепта из файла), состоящая из нескольких операций записи.

```
uint32 max value = 4 294 967 295
int32 max value = 2 147 483 647

int16 max value = 32 767
uint16 max value = 65 535

float32 max value = 3.4028235E+38
```

---

[String в документации Siemens](https://docs.tia.siemens.cloud/r/en-us/v21/data-types/character-strings/character-strings/string)

[WString в документации Siemens](https://docs.tia.siemens.cloud/r/en-us/v21/data-types/character-strings/character-strings/wstring-s7-1200-s7-1500-s7-1200-g2)

[WChar в документации Siemens](https://docs.tia.siemens.cloud/r/en-us/v21/data-types/character-strings/character/wchar-s7-1200-s7-1500-s7-1200-g2)

[Unicode char table](https://symbl.cc/en/unicode-table/#low-surrogates)

Кириллица в unicode в диапазоне 0400–04FF

## Общая информация

Требования протокола:
 - PC — S7 клиент (библиотека S7.Net)
 - PLC — S7 сервер (Siemens S7-1200/S7-1500)

Поддерживается работа с типами данных **int32, float, string** (внешнее представление).

Внутри PLC используются типы Siemens: **WORD**, **DWORD**, **DINT**, **REAL**, **WString**.

Данные разделены по отдельным датаблокам (DB). Номера датаблоков указываются в Header.

Протокол обеспечивает:
- Запись рецепта в PLC
- Чтение рецепта из PLC
- Мониторинг выполнения рецепта в реальном времени

## 1) Header (DB_HEADER)

Header содержит общую информацию о протоколе и номера датаблоков. Читается приложением при инициализации.

Номер датаблока Header задаётся в конфигурации приложения.

| Id | Имя                     | Тип   | Описание |
|----|-------------------------|-------|----------|
| h1 | magic_number (?)        | WORD  | Константа для технических проверок (под вопросом) |
| h2 | word_order_32 (?)       | WORD  | Порядок слов для 32-бит полей (под вопросом, S7 = big-endian) |
| h3 | protocol_version        | WORD  | Версия протокола: `1` |
| h4 | managing_db_number      | WORD  | Номер DB для Managing area |
| h5 | int_data_db_number      | WORD  | Номер DB для INT_ARRAY |
| h6 | float_data_db_number    | WORD  | Номер DB для FLOAT_ARRAY |
| h7 | string_data_db_number   | WORD  | Номер DB для STRING_ARRAY |
| h8 | execution_db_number     | WORD  | Номер DB для Execution info |

## 2) Managing Area (DB_MANAGING)

`Managing area` — область для координации массовой записи и явного commit.

Чтение/запись начинается с адреса 0 внутри датаблока.

| Id  | Имя                  | Тип   | Кто пишет | Описание |
|-----|----------------------|-------|-----------|----------|
| m1  | PC_status            | WORD  | PC        | 0 - idle, 1 - writing, 2 - commit_request |
| m2  | PC_transaction_id    | DWORD | PC        | ID транзакции (уникальный номер) |
| m3  | PC_checksum_int      | DWORD | PC        | Checksum INT_ARRAY (от [0..int_current_size-1]) |
| m4  | PC_checksum_float    | DWORD | PC        | Checksum FLOAT_ARRAY (от [0..float_current_size-1]) |
| m5  | PC_checksum_string   | DWORD | PC        | Checksum STRING_ARRAY (от [0..string_current_size-1]) |
| m6  | PC_recipe_lines      | DWORD | PC        | Количество строк в рецепте |
| m7  | PLC_status           | WORD  | PLC       | 0 - idle, 1 - busy, 2 - crc_computing, 3 - success, 4 - error |
| m8  | PLC_error            | WORD  | PLC       | 0 - no_error, 1 - crc_int, 2 - crc_float, 3 - crc_string, 4 - crc_multiple, 5 - timeout |
| m9  | PLC_stored_id        | DWORD | PLC       | ID последней успешной транзакции |
| m10 | PLC_checksum_int     | DWORD | PLC       | Checksum INT_ARRAY (посчитано PLC) |
| m11 | PLC_checksum_float   | DWORD | PLC       | Checksum FLOAT_ARRAY (посчитано PLC) |
| m12 | PLC_checksum_string  | DWORD | PLC       | Checksum STRING_ARRAY (посчитано PLC) |

### Коды ошибок (PLC_error):
- `0` — no_error
- `1` — checksum_mismatch_int
- `2` — checksum_mismatch_float
- `3` — checksum_mismatch_string
- `4` — checksum_mismatch_multiple (несколько не совпало)
- `5` — timeout (нет активности от PC в течение заданного времени)

### Таймауты:
- **Writing (PC_status = 1):** 5 секунд (время на запись всех данных)
- **Commit (PC_status = 2):** 3 секунды (время на расчет CRC на PLC)
- Таймеры запускаются/перезапускаются по фронтам `PC_status` (0→1 и 1→2)
- После завершения транзакции (success или error) таймеры останавливаются

### Повторные попытки:
При ошибке checksum или timeout: PC повторяет транзакцию с новым `PC_transaction_id`, максимум 3 попытки. После исчерпания попыток — сообщение об ошибке пользователю.

[GetChecksum в документации Siemens](https://docs.tia.siemens.cloud/r/en-us/v21/extended-instructions-s7-1200-s7-1500-s7-1200-g2/diagnostics-s7-1200-s7-1500-s7-1200-g2/getchecksum-read-out-checksum-s7-1200-s7-1500-s7-1200-g2) — ожидается удовлетворительная стоимость подсчёта CRC, можно считать не чаще 1 раза в сек.

## 3) Table Content

Данные рецепта разделены по типам в отдельных датаблоках.

Чтение/запись начинается с адреса 0 внутри каждого датаблока.

### 3.1) INT_ARRAY (DB_INT)

| Тип                  | Имя               | Описание |
|----------------------|-------------------|----------|
| DWORD                | int_area_capacity | Максимальный размер массива (в элементах) |
| DWORD                | int_current_size  | Текущее количество используемых элементов |
| array [0..N] of DINT | int_data          | Массив данных int32 |

**Примечание:** Checksum считается только от элементов `[0..int_current_size-1]`, а не от всего массива.

### 3.2) FLOAT_ARRAY (DB_FLOAT)

| Тип                  | Имя                 | Описание |
|----------------------|---------------------|----------|
| DWORD                | float_area_capacity | Максимальный размер массива (в элементах) |
| DWORD                | float_current_size  | Текущее количество используемых элементов |
| array [0..N] of REAL | float_data          | Массив данных float |

**Примечание:** Checksum считается только от элементов `[0..float_current_size-1]`, а не от всего массива.

### 3.3) STRING_ARRAY (DB_STRING)

| Тип                         | Имя                  | Описание |
|-----------------------------|----------------------|----------|
| DWORD                       | string_area_capacity | Максимальный размер массива (в элементах) |
| DWORD                       | string_current_size  | Текущее количество используемых элементов |
| array [0..N] of WString[32] | string_data          | Массив данных string (макс 32 символа на элемент) |

**Примечание:** Checksum для строк может быть недоступен в зависимости от реализации PLC.

## 4) Execution Info (DB_EXECUTION)

Область для мониторинга выполнения рецепта в реальном времени. PLC записывает данные, PC читает.

Чтение начинается с адреса 0 внутри датаблока.

| Id | Имя               | Тип   | Описание |
|----|-------------------|-------|----------|
| e1 | recipe_active     | BOOL  | Признак, что рецепт исполняется (true = выполняется) |
| e2 | actual_line       | DINT  | Номер текущей исполняемой строки (0-based) |
| e3 | step_current_time | REAL  | Время, прошедшее с начала текущего шага (секунды) |
| e4 | for_loop_count_1  | DINT  | Номер итерации цикла FOR уровня 1 |
| e5 | for_loop_count_2  | DINT  | Номер итерации цикла FOR уровня 2 (вложенный) |
| e6 | for_loop_count_3  | DINT  | Номер итерации цикла FOR уровня 3 (вложенный) |

### Описание полей:

- **recipe_active** — при `true` редактирование таблицы блокируется, операции записи в PLC запрещены.

- **actual_line** — индекс текущего шага (0 = первый шаг). Используется для:
  - Подсветки текущей и пройденных строк в UI
  - Расчёта оставшегося времени

- **step_current_time** — время в секундах с начала текущего шага. Используется для расчёта:
  - `LineTimeLeft = max(0, Длительность_шага − step_current_time)`
  - `TotalTimeLeft = max(0, Общая_длительность − (Время_начала_шага + step_current_time))`

- **for_loop_count_1/2/3** — номера итераций для вложенных циклов FOR:
  - Уровень 1 — внешний цикл
  - Уровень 2 — первый вложенный цикл
  - Уровень 3 — второй вложенный цикл
  - Изменение значения трактуется как смена контекста выполнения

## 5) Сценарии работы

### Workflow транзакции записи:

**Шаг 0:** PC проверяет связь, читает `PLC_status`
- Если `PLC_status = 1` (busy) → ждёт или сообщает ошибку "ПЛК занят"
- Если `PLC_status = 4` (error) → читает `PLC_error`, анализирует ошибку
- Если `PLC_status = 0` (idle) → продолжить

**Шаг 1:** PC устанавливает:
- `PC_status = 1` (writing)
- `PC_transaction_id = random()` (новый уникальный ID)
- `PC_recipe_lines = N` (количество строк в рецепте)
- `PC_checksum_int = checksum(local_int_array[0..int_current_size-1])`
- `PC_checksum_float = checksum(local_float_array[0..float_current_size-1])`
- `PC_checksum_string = checksum(local_string_array[0..string_current_size-1])`

**PLC детектирует фронт 0→1 в `PC_status`:**
- Запускает внутренний таймер (5 секунд на writing)
- Начинает мониторинг транзакции

**Шаг 2:** PC записывает данные в датаблоки (DB_INT, DB_FLOAT, DB_STRING)
- Записываются только изменённые данные или полный массив

**При обрыве связи на этапе 2:**
- PLC: таймер истекает (5 сек без активности)
- PLC устанавливает: `PLC_status = 4` (error), `PLC_error = 5` (timeout)
- Данные помечены как "грязные", не могут использоваться до успешной транзакции

**Шаг 3:** PC устанавливает `PC_status = 2` (commit_request)

**PLC видит переход `PC_status` 1→2:**
- Перезапускает таймер (3 секунды на расчёт CRC)
- Устанавливает `PLC_status = 2` (crc_computing)

**Шаг 4:** PLC считает checksums:
- `PLC_checksum_int = GetChecksum(INT_ARRAY)`
- `PLC_checksum_float = GetChecksum(FLOAT_ARRAY)`
- `PLC_checksum_string = GetChecksum(STRING_ARRAY)` (если доступно)

**Шаг 5:** PLC сравнивает checksums:

**Если совпало:**
- `PLC_status = 3` (success)
- `PLC_stored_id = PC_transaction_id`
- `PLC_error = 0`

**Если НЕ совпало:**
- `PLC_status = 4` (error)
- `PLC_error = 1/2/3/4` (в зависимости от того, какой checksum не совпал)
- `PLC_stored_id` остаётся без изменений

**Если таймер истёк на этапе 4-5:**
- `PLC_status = 4` (error)
- `PLC_error = 5` (timeout)

**Шаг 6:** PC читает `PLC_status` и `PLC_stored_id`:

**Если `PLC_status = 3` AND `PLC_stored_id == PC_transaction_id`:**
- Транзакция успешна
- `PC_status = 0` (idle)

**Если `PLC_status = 4`:**
- Анализ `PLC_error`:
  - `1/2/3/4`: Checksum mismatch → повтор транзакции (новый `transaction_id`), максимум 3 попытки
  - `5`: Timeout → проверка связи, повтор или ошибка пользователю
- `PC_status = 0` (idle)
- При необходимости: новая попытка с шага 0

**Шаг 7:** PLC видит `PC_status = 0` (idle):
- Останавливает таймер
- `PLC_status = 0` (idle)
- Готов к следующей транзакции

### Мониторинг выполнения рецепта:

PC периодически читает DB_EXECUTION для отслеживания прогресса:

1. Проверка `recipe_active`:
   - `true` → рецепт выполняется, UI в режиме мониторинга
   - `false` → рецепт не выполняется, UI в режиме редактирования

2. Обновление UI на основе `actual_line`:
   - Подсветка текущей строки
   - Подсветка пройденных строк
   - Прокрутка к текущей строке (опционально)

3. Расчёт оставшегося времени на основе `step_current_time`:
   - Время до конца текущего шага
   - Время до конца всего рецепта

4. Отображение состояния циклов FOR:
   - Показ текущей итерации для каждого уровня вложенности
