import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import McpTokenPage from './McpTokenPage';

const useGenerateMcpTokenMutationMock = vi.fn();
const toastSuccessMock = vi.fn();
const toastErrorMock = vi.fn();

vi.mock('react-hot-toast', () => ({
    default: { success: (...a: unknown[]) => toastSuccessMock(...a), error: (...a: unknown[]) => toastErrorMock(...a) },
}));
vi.mock('../../store/services/mcpToken', () => ({
    useGenerateMcpTokenMutation: () => useGenerateMcpTokenMutationMock(),
}));

function mockTrigger(result: unknown, shouldThrow = false) {
    return vi.fn(() => ({
        unwrap: () => (shouldThrow ? Promise.reject(result) : Promise.resolve(result)),
    }));
}

function stubClipboard(writeText: (text: string) => Promise<void>) {
    Object.defineProperty(navigator, 'clipboard', {
        writable: true,
        configurable: true,
        value: { writeText: vi.fn(writeText) },
    });
}

describe('McpTokenPage', () => {
    beforeEach(() => {
        toastSuccessMock.mockClear();
        toastErrorMock.mockClear();
        useGenerateMcpTokenMutationMock.mockReturnValue([mockTrigger({ isSuccess: true, value: 'my-generated-token' }), { isLoading: false }]);
    });

    it('renders the generate button and no token field initially', () => {
        render(<McpTokenPage />);

        expect(screen.getByRole('button', { name: 'generate.button' })).toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'token.copyButton' })).not.toBeInTheDocument();
    });

    it('generates a token, displays it, and shows a success toast', async () => {
        render(<McpTokenPage />);

        await userEvent.click(screen.getByRole('button', { name: 'generate.button' }));

        await waitFor(() => expect(toastSuccessMock).toHaveBeenCalledWith('toasts.generateSuccess'));
        expect(screen.getByDisplayValue('my-generated-token')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'generate.regenerateButton' })).toBeInTheDocument();
    });

    it('shows an error toast when the mutation throws', async () => {
        useGenerateMcpTokenMutationMock.mockReturnValue([mockTrigger(new Error('network down'), true), { isLoading: false }]);
        render(<McpTokenPage />);

        await userEvent.click(screen.getByRole('button', { name: 'generate.button' }));

        await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith('toasts.generateError'));
        expect(screen.queryByRole('button', { name: 'token.copyButton' })).not.toBeInTheDocument();
    });

    it('shows the server-provided error message when the result is not successful', async () => {
        useGenerateMcpTokenMutationMock.mockReturnValue([
            mockTrigger({ isSuccess: false, errors: [{ message: 'No bearer token on the current request.' }] }),
            { isLoading: false },
        ]);
        render(<McpTokenPage />);

        await userEvent.click(screen.getByRole('button', { name: 'generate.button' }));

        await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith('No bearer token on the current request.'));
    });

    it('disables the generate button while loading', () => {
        useGenerateMcpTokenMutationMock.mockReturnValue([mockTrigger({ isSuccess: true, value: 'x' }), { isLoading: true }]);
        render(<McpTokenPage />);

        expect(screen.getByRole('button', { name: 'generate.generating' })).toBeDisabled();
    });

    it('copies the token to the clipboard and shows a success toast', async () => {
        stubClipboard(() => Promise.resolve());
        render(<McpTokenPage />);
        await userEvent.click(screen.getByRole('button', { name: 'generate.button' }));
        await waitFor(() => expect(screen.getByDisplayValue('my-generated-token')).toBeInTheDocument());

        await userEvent.click(screen.getByRole('button', { name: 'token.copyButton' }));

        expect(navigator.clipboard.writeText).toHaveBeenCalledWith('my-generated-token');
        await waitFor(() => expect(toastSuccessMock).toHaveBeenCalledWith('toasts.copySuccess'));
        expect(screen.getByRole('button', { name: 'token.copiedButton' })).toBeInTheDocument();
    });

    it('shows an error toast when copying to the clipboard fails', async () => {
        stubClipboard(() => Promise.reject(new Error('denied')));
        render(<McpTokenPage />);
        await userEvent.click(screen.getByRole('button', { name: 'generate.button' }));
        await waitFor(() => expect(screen.getByDisplayValue('my-generated-token')).toBeInTheDocument());

        await userEvent.click(screen.getByRole('button', { name: 'token.copyButton' }));

        await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith('toasts.copyError'));
    });

    it('shows the MCP server URL derived from the current origin in the instructions', () => {
        render(<McpTokenPage />);

        expect(screen.getByText(`${window.location.origin}/mcp`)).toBeInTheDocument();
    });
});
