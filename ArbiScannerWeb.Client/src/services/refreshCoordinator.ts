/**
 * Shared refresh coordinator — ensures only one token-refresh call is
 * in-flight at any time, regardless of whether the caller is an axios
 * interceptor or an RTK-Query base query.
 *
 * Any caller that arrives while a refresh is already in progress is queued
 * and will receive the same new access token once the in-flight refresh
 * resolves, then retry its own request.
 */

type PendingEntry = {
    resolve: (token: string) => void;
    reject: (error: unknown) => void;
};

let isRefreshing = false;
let pendingQueue: PendingEntry[] = [];

function flushQueue(error: unknown, token: string | null): void {
    pendingQueue.forEach(({ resolve, reject }) => {
        if (error != null || token == null) {
            reject(error);
        } else {
            resolve(token);
        }
    });
    pendingQueue = [];
}

/**
 * Runs `doRefresh` exactly once while concurrent callers wait in a queue.
 * Returns the new access token on success; throws (and propagates to all
 * queued callers) on failure.
 *
 * @param doRefresh  An async function that performs the actual refresh API
 *                   call and returns the new access token string.
 */
export async function coordinatedRefresh(doRefresh: () => Promise<string>): Promise<string> {
    if (isRefreshing) {
        return new Promise<string>((resolve, reject) => {
            pendingQueue.push({ resolve, reject });
        });
    }

    isRefreshing = true;
    try {
        const token = await doRefresh();
        flushQueue(null, token);
        return token;
    } catch (err) {
        flushQueue(err, null);
        throw err;
    } finally {
        isRefreshing = false;
    }
}
