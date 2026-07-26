import { Component, type ErrorInfo, type ReactNode } from 'react';
import { logger } from '../services/loggerService';

interface Props {
    children: ReactNode;
}

interface State {
    hasError: boolean;
    error: Error | null;
}

class ErrorBoundary extends Component<Props, State> {
    constructor(props: Props) {
        super(props);
        this.state = { hasError: false, error: null };
    }

    static getDerivedStateFromError(error: Error): State {
        return { hasError: true, error };
    }

    componentDidCatch(error: Error, info: ErrorInfo) {
        logger.error(
            error.message,
            'ErrorBoundary',
            info.componentStack ?? undefined
        );
    }

    render() {
        if (this.state.hasError) {
            return (
                <div className="flex flex-col items-center justify-center min-h-screen gap-4 p-8 text-center">
                    <h1 className="text-2xl font-semibold">Something went wrong</h1>
                    <p className="text-gray-500">An unexpected error occurred. Please refresh the page.</p>
                    <button type="button"
                        className="px-4 py-2 rounded bg-blue-600 text-white hover:bg-blue-700"
                        onClick={() => window.location.reload()}
                    >
                        Refresh
                    </button>
                </div>
            );
        }

        return this.props.children;
    }
}

export default ErrorBoundary;
