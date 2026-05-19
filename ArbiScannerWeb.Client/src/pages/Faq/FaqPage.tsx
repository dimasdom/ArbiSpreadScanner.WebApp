import FaqElement from './FaqElement';

const faqData = [
    {
        question: "What is ArbiScanner?",
        answer: "ArbiScanner is a comprehensive web trading application designed to help you identify and capitalize on spread opportunities. We support various spread types including funding rates, spot arbitrage, and futures spreads across multiple major exchanges."
    },
    {
        question: "Which exchanges are currently supported?",
        answer: (
            <span>
                We currently support a wide range of top-tier exchanges including
                <span className="font-semibold text-gray-800"> Binance, Bybit, OKX, KuCoin Futures, MEXC, Bitget, HTX, XT, CoinEX, LBank, WhiteBit, Gate.io, and BingX</span>.
                We are constantly working on adding more integrations.
            </span>
        )
    },
    {
        question: "How do Smart Alerts work?",
        answer: "Smart Alerts allow you to set specific threshold criteria for spreads. Once a spread meets your minimum profit requirements, you will be notified instantly via the dashboard or configured notification channels, ensuring you never miss a profitable opportunity."
    },
    {
        question: "Is there a risk management system?",
        answer: "Yes! ArbiScanner includes built-in risk management tools. You can define custom position sizing rules that automatically calculate trade amounts based on your total portfolio value and personal risk tolerance."
    },
    {
        question: "Can I customize which exchanges I see?",
        answer: "Absolutely. Our 'Personalize Experience' feature lets you toggle specific exchanges on or off. If you only have accounts with or trust certain exchanges, you can filter the dashboard to show only relevant data."
    },
    {
        question: "What does the Spread Size Threshold mean?",
        answer: (
            <span>
                The <strong>Spread Size Threshold</strong> (set in Account Settings) is the minimum spread percentage you want to be notified about. For example, setting it to <strong>0.5</strong> means you will only see spreads of 0.5% or higher. This filters out small, unprofitable opportunities and reduces noise in the feed.
            </span>
        )
    },
    {
        question: "What are Short Exchange and Long Exchange?",
        answer: (
            <span>
                Each spread involves two opposite positions placed simultaneously:
                <br /><br />
                <strong>Short Exchange</strong> — the exchange where the asset price is higher. You sell (short) the asset here.
                <br />
                <strong>Long Exchange</strong> — the exchange where the asset price is lower. You buy (long) the asset here.
                <br /><br />
                The profit comes from the price difference between the two sides, minus trading fees.
            </span>
        )
    },
    {
        question: "How do I read the order book on the Spread detail page?",
        answer: (
            <span>
                The order book shows the current <strong>asks</strong> (sell orders) and <strong>bids</strong> (buy orders) for each exchange.
                <br /><br />
                <strong>Asks</strong> — prices sellers are willing to accept. You will buy at the ask on the Long Exchange.
                <br />
                <strong>Bids</strong> — prices buyers are willing to pay. You will sell at the bid on the Short Exchange.
                <br /><br />
                Look for large order sizes near your target price — this means better <strong>liquidity</strong> and less <strong>slippage</strong> when entering or exiting the trade.
            </span>
        )
    },
    {
        question: "What does Volatility mean on the Spread page?",
        answer: (
            <span>
                <strong>Volatility</strong> measures how much the price has fluctuated recently across both exchanges. It is calculated from the live ticker data in the current session.
                <br /><br />
                Higher volatility means prices are moving rapidly — this can mean faster spread convergence (quicker profit) but also higher risk of the spread widening further against you.
                Lower volatility usually indicates a more stable, predictable spread.
            </span>
        )
    },
    {
        question: "How do Funding Rate spreads work?",
        answer: (
            <span>
                In perpetual futures markets, exchanges charge a periodic <strong>funding rate</strong> between long and short positions to keep the price anchored to the spot market.
                <br /><br />
                When the funding rate is <strong>positive</strong>: longs pay shorts. The Short side <em>receives</em> funding; the Long side <em>pays</em> funding.
                <br />
                When the funding rate is <strong>negative</strong>: shorts pay longs. The Long side <em>receives</em> funding.
                <br /><br />
                ArbiScanner displays which direction you pay or receive funding directly on the Spread detail page.
            </span>
        )
    }
];

function FaqPage() {
    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <div className="bg-white shadow-2xl rounded-3xl overflow-hidden">
                {/* Header Section */}
                <div className="bg-gradient-to-r from-blue-600 to-indigo-700 py-10 sm:py-16 px-4 sm:px-8 text-center text-white">
                    <h1 className="text-2xl sm:text-4xl md:text-6xl font-extrabold mb-4 sm:mb-6 tracking-tight">
                        Frequently Asked Questions
                    </h1>
                    <p className="text-base sm:text-xl md:text-2xl max-w-3xl mx-auto opacity-90 leading-relaxed">
                        Everything you need to know about 
                        <span className="font-semibold text-yellow-300"> ArbiScanner</span>, 
                        features, and how to get started with spread trading.
                    </p>
                </div>

                {/* FAQ Content Section */}
                <div className="p-8 md:p-12">
                    <div className="max-w-4xl mx-auto bg-white">
                        <div className="space-y-1">
                            {faqData.map((faq, index) => (
                                <FaqElement 
                                    key={index} 
                                    question={faq.question} 
                                    answer={faq.answer} 
                                />
                            ))}
                        </div>
                    </div>
                    
                    {/* Contact Support CTA */}
                    <div className="mt-16 text-center border-t border-gray-100 pt-10">
                        <p className="text-gray-600 mb-4">
                            Can't find what you're looking for?
                        </p>
                        <a
                            href="mailto:arbiscannerweb@atomicmail.io"
                            className="inline-block bg-indigo-600 text-white px-8 py-3 rounded-full font-semibold shadow-lg hover:bg-indigo-700 transition-all transform hover:-translate-y-0.5"
                        >
                            Contact Support
                        </a>
                        <p className="text-sm text-gray-400 mt-3">
                            Send us an email at <span className="text-indigo-600 font-medium">arbiscannerweb@atomicmail.io</span>
                        </p>
                    </div>

                    {/* Reset guides */}
                    <div className="mt-8 text-center">
                        <button
                            onClick={() => {
                                localStorage.removeItem('guides-all-disabled');
                                const keys = Object.keys(localStorage).filter(k => k !== 'guides-all-disabled');
                                keys.forEach(k => {
                                    if (localStorage.getItem(k) === 'true') localStorage.removeItem(k);
                                });
                                globalThis.location.reload();
                            }}
                            className="inline-flex items-center gap-2 text-sm bg-indigo-600 text-white hover:bg-indigo-700 transition-colors rounded-full px-4 py-2"
                        >
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                            </svg>
                            See guides again
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default FaqPage;