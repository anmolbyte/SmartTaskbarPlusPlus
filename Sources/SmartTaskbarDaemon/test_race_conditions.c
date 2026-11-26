#include <windows.h>
#include <stdio.h>
#include <stdbool.h>
#include "settings.h"

#define NUM_THREADS 8
#define OPS_PER_THREAD 50

static volatile LONG g_totalReads = 0;
static volatile LONG g_totalWrites = 0;
static volatile LONG g_totalErrors = 0;

DWORD WINAPI WorkerThread(LPVOID lpParam) {
    int id = (int)(intptr_t)lpParam;
    for (int i = 0; i < OPS_PER_THREAD; i++) {
        // Alternating read and write
        if (i % 2 == 0) {
            AppSettings s;
            if (settings_load(&s)) {
                InterlockedIncrement(&g_totalReads);
            } else {
                InterlockedIncrement(&g_totalErrors);
            }
        } else {
            AppSettings s;
            settings_get_default(&s);
            s.is_negative_enabled = (id % 2 == 0);
            s.auto_mode = ((i + id) % 2 == 0) ? AUTO_MODE_AUTO : AUTO_MODE_NONE;
            s.large_screen_threshold = 20.0 + (double)(id * 2);
            if (settings_save(&s)) {
                InterlockedIncrement(&g_totalWrites);
            } else {
                InterlockedIncrement(&g_totalErrors);
            }
        }
        Sleep(1);
    }
    return 0;
}

int main(void) {
    printf("=====================================================\n");
    printf("SmartTaskbarDaemon Race Condition & Concurrency Test\n");
    printf("=====================================================\n");
    printf("Launching %d concurrent threads executing %d operations each...\n", NUM_THREADS, OPS_PER_THREAD);

    HANDLE hThreads[NUM_THREADS];
    for (int i = 0; i < NUM_THREADS; i++) {
        hThreads[i] = CreateThread(NULL, 0, WorkerThread, (LPVOID)(intptr_t)i, 0, NULL);
        if (!hThreads[i]) {
            printf("[FAIL] Failed to create thread %d\n", i);
            return 1;
        }
    }

    WaitForMultipleObjects(NUM_THREADS, hThreads, TRUE, 30000);

    for (int i = 0; i < NUM_THREADS; i++) {
        CloseHandle(hThreads[i]);
    }

    printf("\nTest Results:\n");
    printf("  Total Reads:  %ld\n", g_totalReads);
    printf("  Total Writes: %ld\n", g_totalWrites);
    printf("  Total Errors: %ld\n", g_totalErrors);

    if (g_totalErrors == 0 && (g_totalReads + g_totalWrites) == (NUM_THREADS * OPS_PER_THREAD)) {
        printf("\n[SUCCESS] All %d operations completed with 0 race condition errors or lock failures!\n", NUM_THREADS * OPS_PER_THREAD);
        return 0;
    } else {
        printf("\n[FAILURE] Encountered %ld errors during concurrent execution!\n", g_totalErrors);
        return 1;
    }
}
