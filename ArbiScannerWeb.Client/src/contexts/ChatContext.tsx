import React, { createContext, useContext, useMemo, useState } from 'react';

interface ChatContextValue {
    // The spread the user currently has open (SpreadPage sets this on mount,
    // clears it on unmount), so the chat widget can offer to analyze "this"
    // spread without the user typing an id.
    currentSpreadId: string | null;
    setCurrentSpreadId: (id: string | null) => void;
}

const ChatContext = createContext<ChatContextValue | undefined>(undefined);

export const ChatContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [currentSpreadId, setCurrentSpreadId] = useState<string | null>(null);
    const value = useMemo<ChatContextValue>(
        () => ({ currentSpreadId, setCurrentSpreadId }),
        [currentSpreadId],
    );

    return <ChatContext.Provider value={value}>{children}</ChatContext.Provider>;
};

export function useChatContext(): ChatContextValue {
    const context = useContext(ChatContext);
    if (!context) {
        throw new Error('useChatContext must be used within a ChatContextProvider');
    }
    return context;
}
