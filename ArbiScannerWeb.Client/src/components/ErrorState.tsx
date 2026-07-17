interface ErrorStateProps {
    message?: string;
    onRetry?: () => void;
}

function ErrorState({ message = 'Something went wrong. Please try again later.', onRetry }: ErrorStateProps) {
    return (
        <div className="flex h-[80vh] flex-col items-center justify-center gap-4">
            <div className="text-xl text-red-500 font-semibold bg-red-50 dark:bg-red-900/20 px-6 py-4 rounded-xl border border-red-200 dark:border-red-800">
                {message}
            </div>
            {onRetry && (
                <button
                    type="button"
                    onClick={onRetry}
                    className="text-sm font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400"
                >
                    Try again
                </button>
            )}
        </div>
    );
}

export default ErrorState;
